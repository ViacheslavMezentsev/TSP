using System.Collections.Generic;
using System.Linq;

namespace TSP
{
    public class Tour
    {

        #region Constructors

        public Tour( List<City> list ) => Cities = list;

        #endregion

        #region Public methods

        public static Tour Random( int n ) => new Tour( Enumerable.Range( 0, n ).Select( x => City.Random() ).ToList() );

        public Tour Shuffle()
        {
            var tour = new List<City>( Cities );

            int n = tour.Count;

            while ( n > 1 )
            {
                n--;

                int k = Program.rand.Next( n + 1 );

                var v = tour[k];
                tour[k] = tour[n];
                tour[n] = v;
            }

            return new Tour( tour );
        }

        public Tour Crossover( Tour tour )
        {
            var i = Program.rand.Next( 0, tour.Cities.Count );
            var j = Program.rand.Next( i, tour.Cities.Count );

            var s = Cities.GetRange( i, j - i + 1 );

            var ms = tour.Cities.Except(s).ToList();

            var c = ms.Take(i).Concat(s).Concat( ms.Skip(i) ).ToList();

            return new Tour(c);
        }

        public Tour Mutate()
        {
            var cities = new List<City>( Cities );

            if ( Program.rand.NextDouble() < Env.MutRate )
            {
                var i = Program.rand.Next( 0, Cities.Count );
                var j = Program.rand.Next( 0, Cities.Count );

                var v = cities[i];
                cities[i] = cities[j];
                cities[j] = v;
            }

            return new Tour( cities );
        }

        #endregion

        #region Properties

        public List<City> Cities { get; }

        public double Distance => Cities.Select( ( t, i ) => t.DistanceTo( Cities[ ( i + 1 ) % Cities.Count ] ) ).Sum();

        public double Fitness => 1.0 / Distance;

        #endregion

    }
}
