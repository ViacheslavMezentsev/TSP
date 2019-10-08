using System;

namespace TSP
{
    public class City
    {

        #region Private fields

        private const double EarthDiam = 2d * 6378.1370d;

        #endregion

        #region Constructors

        public City( string name, double x, double y )
        {
            Name = name;
            this.x = x;
            this.y = y;
        }

        #endregion

        #region Public methods

        // https://en.wikipedia.org/wiki/Haversine_formula
        public double DistanceTo( City city )
        {
            var a = 0.5 * ( 1d - Math.Cos( city.x - x ) + Math.Cos(x) * Math.Cos( city.x ) * ( 1d - Math.Cos( city.y - y ) ) );

            return EarthDiam * Math.Asin( Math.Sqrt(a) );
        }

        public static City Random() => new City( "", Program.rand.NextDouble(), Program.rand.NextDouble() );

        #endregion

        #region Properties

        public string Name;
        public double x { get; set; }
        public double y { get; set; }

        #endregion

    }
}
