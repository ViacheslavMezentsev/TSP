using System.Collections.Generic;
using System.Linq;

namespace TSP
{
    public class Population
    {

        #region Constructors

        public Population( List<Tour> list ) => Tours = list;

        #endregion

        #region Public methods        

        public static Population Randomized( Tour t, int n ) => new Population( Enumerable.Range( 0, n ).Select( x => t.Shuffle() ).ToList() );

        public Tour Select()
        {
            while ( true )
            {
                int i = Program.rand.Next( 0, Env.PopSize );

                if ( Program.rand.NextDouble() < Tours[i].Fitness / MaxFit )
                {
                    return new Tour( Tours[i].Cities );
                }
            }
        }

        public Population GenNewPop( int n )
        {
            var list = new List<Tour>();

            for ( int i = 0; i < n; ++i )
            {
                var t = Select().Crossover( Select() );

                foreach ( var city in t.Cities ) t = t.Mutate();

                list.Add(t);
            }

            return new Population( list );
        }

        public Population Elite( int n )
        {
            var best = new List<Tour>();
            var tmp = new Population( Tours );

            for ( int i = 0; i < n; ++i )
            {
                best.Add( tmp.FindBest() );

                tmp = new Population( tmp.Tours.Except( best ).ToList() );
            }

            return new Population( best );
        }

        public Tour FindBest() => Tours.FirstOrDefault( t => t.Fitness == MaxFit );

        public Population Evolve()
        {
            var best = Elite( Env.Elitism );

            var np = GenNewPop( Env.PopSize - Env.Elitism );

            return new Population( best.Tours.Concat( np.Tours ).ToList() );
        }

        #endregion

        #region Properties

        public List<Tour> Tours { get; }

        public double MaxFit => Tours.Max( t => t.Fitness );

        #endregion

    }
}
