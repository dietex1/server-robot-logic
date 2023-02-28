using System.Text.RegularExpressions;

namespace PSI_BOUDA
{
    public class Mover
    {

        class Coordinates
        {
            public int y;
            public int x;

            public Coordinates(int x, int y)
            {
                this.y = y;
                this.x = x;
            }

            public bool Equals(Coordinates other)
            {
                return x == other.x && y == other.y;
            }
        }

        private static readonly string MOVE = "102 MOVE\a\b";

        private static readonly string TURN_RIGHT = "104 TURN RIGHT\a\b";

        class Direct
        {

            public const int DOWN = 2;
            public const int LEFT = 3;
            public const int NONE = 4;
            public const int UP = 0;
            public const int RIGHT = 1;


            public Direct(int state)
            {
                if (state < 0 || state > 4)
                {
                    throw new ArgumentException();
                }
                State = state;
            }

            public void Next()
            {
                Increment();
                if (State.Equals(NONE)) Increment();
            }

            public int State { get; set; }

            private void Decrement()
            {
                if (State.Equals(0))
                {
                    State = 4;
                    return;
                }
                State--;
            }

            private void Increment()
            {
                State = (State + 1) % 5;
            }

            public void Previous()
            {
                Decrement();
                if (State == NONE)
                {
                    Decrement();
                }
            }


            public bool Equals(Direct other)
            {
                return State.Equals(other.State);
            }

            public bool Equals(int state)
            {
                return State.Equals(state);
            }

        }

        public Mover(Client client)
        {
            networkWrapper = new ChargerWrapper(client.netWrapper);
            statusWrapper = new StatusWrapper(networkWrapper);
        }

        private readonly NetworkWrapper networkWrapper;

        private readonly StatusWrapper statusWrapper;

        private static readonly string TURN_LEFT = "103 TURN LEFT\a\b";

        private static readonly string OK_REGEX = @"^OK -?\d+ -?\d+$";

        private Coordinates coordinates;

        private Direct direction;

        public void Go()
        {
            try
            {
                Coordinates oldCoordinates;
                Move();
                oldCoordinates = coordinates;
                Move();
                GetDirection(oldCoordinates);

                if (hasReachedCentr())
                {
                    return;
                }

                if (direction.Equals(Direct.NONE))
                {
                    UrgentDirDetection();
                }

                switch (GetQuadrant())
                {
                    case 1:
                        while (!direction.Equals(Direct.DOWN)) turnRight();
                        break;
                    case 2:
                        while (!direction.Equals(Direct.DOWN)) turnRight();
                        break;
                    case 3:
                        while (!direction.Equals(Direct.UP)) turnRight();
                        break;
                    case 4:
                        while (!direction.Equals(Direct.UP)) turnRight();
                        break;
                    case -1:
                        FixxingDirection();
                        break;
                    default:
                        break;
                }


                Coordinates oldCoords2;
                while (!isAxis())
                {
                    oldCoords2 = coordinates;
                    Move();
                    if (coordinates.Equals(oldCoords2))
                    {
                        MoveThroughBar(false);
                        continue;
                    }
                }

                if (hasReachedCentr()) return;

                FixxingDirection();


                Coordinates oldCoords3;
                while (coordinates.x != 0 || coordinates.y != 0)
                {
                    oldCoords3 = coordinates;
                    Move();
                    if (coordinates.Equals(oldCoords3))
                    {
                        MoveThroughBar(true);
                        continue;
                    }
                }

                networkWrapper.Send("105 GET MESSAGE\a\b");
                networkWrapper.Rec(100, out string result);

                return;
            }
            catch (InvalidResponseEx)
            {
                statusWrapper.SyntaxError();
            }
            throw new WorkEndEx();
        }

        private void MoveThroughBar(bool wasOnAxis)
        {
            turnRight();
            Move();
            if (isAxis() && !wasOnAxis) return;
            turnLeft();
            Move();
            if (isAxis() && !wasOnAxis) return;

            Move();
            if (isAxis() && !wasOnAxis) return;
            turnLeft();
            Move();
            if (isAxis() && !wasOnAxis) return;
            turnRight();
        }

        private void FixxingDirection()
        {
            if (!coordinates.x.Equals(0) && !coordinates.y.Equals(0)) throw new ArgumentException();

            if (coordinates.x.Equals(0))
            {
                if (coordinates.y > 0)
                    while (!direction.Equals(Direct.DOWN)) turnRight();
                if (coordinates.y < 0)
                    while (!direction.Equals(Direct.UP)) turnRight();
            }
            if (coordinates.y.Equals(0))
            {
                if (coordinates.x > 0)
                    while (!direction.Equals(Direct.LEFT)) turnRight();
                if (coordinates.x < 0)
                    while (!direction.Equals(Direct.RIGHT)) turnRight();
            }
        }

        private void GetDirection(Coordinates oldCoordinates)
        {
            if (coordinates.x > oldCoordinates.x)
            {
                direction = new Direct(Direct.RIGHT);
                return;
            }
            if (coordinates.x < oldCoordinates.x)
            {
                direction = new Direct(Direct.LEFT);
                return;
            }
            if (coordinates.y > oldCoordinates.y)
            {
                direction = new Direct(Direct.UP);
                return;
            }
            if (coordinates.y < oldCoordinates.y)
            {
                direction = new Direct(Direct.DOWN);
                return;
            }
            direction = new Direct(Direct.NONE);
        }

        private void UrgentDirDetection()
        {
            Coordinates oldCoordinates = coordinates;
            Move();
            if (oldCoordinates.Equals(coordinates))
            {
                turnRight();
            }
            Move();
            GetDirection(oldCoordinates);
        }

        private int GetQuadrant()
        {
            if (coordinates.x > 0 && coordinates.y > 0)
            {
                return 1;
            }
            if (coordinates.x < 0 && coordinates.y > 0)
            {
                return 2;
            }
            if (coordinates.x < 0 && coordinates.y < 0)
            {
                return 3;
            }
            if (coordinates.x > 0 && coordinates.y < 0)
            {
                return 4;
            }
            return -1;
        }

        private bool hasReachedCentr() { return coordinates.x.Equals(0) && coordinates.y.Equals(0); }

        private bool isAxis() { return coordinates.x.Equals(0) || coordinates.y.Equals(0); }

        private void Move()
        {
            networkWrapper.Send(MOVE);

            networkWrapper.Rec(12, out string response);
            if (!Regex.IsMatch(response, OK_REGEX))
                throw new InvalidResponseEx(InvalidResponseEx.INV_COORDINATES);

            string[] responseArr = response.Split(' ');
            coordinates = new Coordinates(int.Parse(responseArr[1]), int.Parse(responseArr[2]));
        }

        private void turnRight()
        {
            networkWrapper.Send(TURN_RIGHT);

            networkWrapper.Rec(12, out string response);
            if (!Regex.IsMatch(response, OK_REGEX))
                throw new Exception();

            direction.Next();
        }

        private void turnLeft()
        {
            networkWrapper.Send(TURN_LEFT);

            networkWrapper.Rec(12, out string response);
            if (!Regex.IsMatch(response, OK_REGEX))
                throw new Exception();

            direction.Previous();
        }



    }
}

