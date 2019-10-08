using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using TSP;

// Описание генетического алгоритма.
// Задача кодируется таким образом, чтобы её решение могло быть представлено в виде вектора ("хромосома").
// Случайным образом создаётся некоторое количество начальных векторов ("начальная популяция").
// Они оцениваются с использованием "функции приспособленности", в результате чего каждому вектору присваивается 
// определённое значение ("приспособленность"), которое определяет вероятность выживания организма, представленного 
// данным вектором. После этого с использованием полученных значений приспособленности выбираются вектора (селекция), 
// допущенные к "скрещиванию". К этим векторам применяются "генетические операторы" (в большинстве случаев 
// "скрещивание" - crossover и "мутация" - mutation), создавая таким образом следующее "поколение". 
// Особи следующего поколения также оцениваются, затем производится селекция, применяются генетические операторы и т.д.
// Так моделируется "эволюционный процесс", продолжающийся несколько жизненных циклов (поколений), 
// пока не будет выполнен критерий останова алгоритма.
public class Program
{
    private static double _d2r = Math.PI / 180d;
    public static string AssemblyDirectory => Path.GetDirectoryName( new Uri( Assembly.GetExecutingAssembly().CodeBase ).LocalPath );

    public static Random rand { get; private set; }

    private static bool _verbose;
    private static int _interval;
    private static int _generations;
    private static double _length;
    private static string _format = "csv";

    public static void Main( string[] args )
    {
        var cult = Thread.CurrentThread.CurrentCulture;

        Thread.CurrentThread.CurrentCulture = new CultureInfo( cult.IetfLanguageTag )
        {
            NumberFormat = { NumberDecimalSeparator = "." }
        };

        rand = new Random();

        // Допустимые параметры:
        // -t10(s|m|h) - завершение работы по истечении указанного интервала;
        // -g5000 - заверешение работы при достижении указанного количества поколений;
        // -l80(km) - завершение работы при достижении указанной протяженности маршрута;
        // -p20 - размер популяции;
        // -m2.5 - процент мутаций;
        // -e5 - количество выживающих лучших особей популяции;
        // -o(gpx|csv) - формат выходного файла.

        var files = new List<string>();

        foreach ( var arg in args )
        {
            if ( !new Regex( @"(-\w+|--\w+)" ).IsMatch( arg ) ) files.Add( arg );

            // Verbose parameter.
            else if ( arg.Equals( "-v" ) ) _verbose = true;

            // Number of generations.
            else if ( arg.Contains( "-g" ) ) int.TryParse( arg.Replace( "-g", "" ), out _generations );

            // The size of population.
            else if ( arg.Contains( "-p" ) ) int.TryParse( arg.Replace( "-p", "" ), out Env.PopSize );

            // Mutation rate.
            else if ( arg.Contains( "-m" ) )
            {
                if ( double.TryParse( arg.Replace( "-m", "" ), out Env.MutRate ) ) Env.MutRate /= 100d;
            }

            // Elitism value.
            else if ( arg.Contains( "-e" ) ) int.TryParse( arg.Replace( "-e", "" ), out Env.Elitism );

            // Output format.
            else if ( arg.Contains( "-o" ) ) _format = arg.Replace( "-o", "" );

            // Time interval.
            else if ( arg.Contains( "-t" ) )
            {
                if ( arg.Contains( "s" ) )
                {
                    int.TryParse( arg.Replace( "-t", "" ).Replace( "s", "" ), out _interval );
                }
                else if ( arg.Contains( "m" ) )
                {
                    if ( int.TryParse( arg.Replace( "-t", "" ).Replace( "m", "" ), out _interval ) ) _interval *= 60;
                }
                else if ( arg.Contains( "h" ) )
                {
                    if ( int.TryParse( arg.Replace( "-t", "" ).Replace( "h", "" ), out _interval ) ) _interval *= 3600;
                }
                else
                {
                    int.TryParse( arg.Replace( "-t", "" ), out _interval );
                }
            }

            // Route length.
            else if ( arg.Contains( "-l" ) )
            {
                if ( arg.Contains( "km" ) )
                {
                    double.TryParse( arg.Replace( "-l", "" ).Replace( "km", "" ), out _length );
                }
                else
                {
                    double.TryParse( arg.Replace( "-l", "" ), out _length );
                }
            }
        }

        foreach ( var task in files )
        {
            try
            {
                Evolution( task );
            }
            catch ( Exception ex )
            {
                Console.WriteLine( ex );
            }
        }
    }

    // https://stackoverflow.com/a/35046453/2467943
    private static bool IsFullPath( string path )
    {
        return !string.IsNullOrWhiteSpace( path )
               && path.IndexOfAny( Path.GetInvalidPathChars().ToArray() ) == -1
               && Path.IsPathRooted( path )
               && !Path.GetPathRoot( path ).Equals( Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal );
    }

    private static void Evolution( string task )
    {
        if ( !IsFullPath( task ) ) task = Path.Combine( AssemblyDirectory, task );

        // Load task from file.
        var lines = File.ReadAllLines( task );

        var cities = new List<City>();

        foreach ( var line in lines )
        {
            var items = line.Split( ',' );

            cities.Add( new City( items[0].Trim(), double.Parse( items[1].Trim() ), double.Parse( items[2].Trim() ) ) );
        }

        var route = cities.Select( ( city, n ) => new City( n.ToString(), _d2r * city.x, _d2r * city.y ) ).ToList();

        var dest = new Tour( route );

        var p = Population.Randomized( dest, Env.PopSize );

        int gen = 0;
        bool better = true;

        Tour best = null;

        var time = new Stopwatch();

        time.Start();

        while ( true )
        {
            if ( _interval > 0 && time.ElapsedMilliseconds > _interval * 1000 ) break;

            if ( Console.KeyAvailable ) break;

            if ( _generations > 0 && gen > _generations ) break;

            if ( better )
            {
                best = p.FindBest();

                if ( _verbose ) Display( best, gen );

                if ( _length > 0 && best.Distance < _length ) break;
            }

            better = false;
            var oldFit = p.MaxFit;

            p = p.Evolve();

            if ( p.MaxFit > oldFit ) better = true;

            gen++;
        }

        time.Stop();

        if ( _verbose ) Console.WriteLine( $"Time: {time.ElapsedMilliseconds / 1000} sec" );

        if ( _format == "gpx" )
        {
            SaveToGPX( task, cities, best );
        }
        else
        {
            SaveToCSV( task, cities, best );
        }
    }

    public static void Display( Tour best, int gen )
    {        
        Console.WriteLine( $"Generation {gen}:\n" + $"Best fitness = {best.Fitness}\n" + $"Best distance = {best.Distance}\n" );
    }

    public static void SaveToCSV( string task, List<City> cities, Tour tour)
    {
        if ( tour == null ) return;

        for ( var n = 0; n < tour.Cities.Count; n++ )
        {
            var city = tour.Cities[0];

            if ( city.Name == "0" ) break;

            tour.Cities.Add( city );
            tour.Cities.RemoveAt(0);
        }

        var route = new List<City>();

        foreach ( var city in tour.Cities )
        {
            var n = int.Parse( city.Name );

            route.Add( cities[n] );
        }

        var text = string.Empty;

        foreach ( var city in route )
        {
            text += $@"{city.Name},{city.x},{city.y}{Environment.NewLine}";
        }

        try
        {
            var name = Path.Combine( AssemblyDirectory, Path.GetFileNameWithoutExtension( task ) ) + $"-{tour.Distance:F1}km.csv";

            File.WriteAllText( name, text, Encoding.UTF8 );
        }
        catch { }
    }

    public static void SaveToGPX( string task, List<City> cities, Tour tour )
    {
        if ( tour == null ) return;

        for ( var n = 0; n < tour.Cities.Count; n++ )
        {
            var city = tour.Cities[0];

            if ( city.Name == "0" ) break;

            tour.Cities.Add( city );
            tour.Cities.RemoveAt(0);
        }

        var route = new List<City>();

        foreach ( var city in tour.Cities )
        {
            var n = int.Parse( city.Name );

            route.Add( cities[n] );
        }

        route.Add( new City( route[0].Name, route[0].x, route[0].y ) );

        var text = @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""no""?>" + Environment.NewLine;

        text += @"<gpx>" + Environment.NewLine;

        foreach ( var city in cities )
        {
            text += $@"<wpt lat=""{city.x}"" lon=""{city.y}""><name>{city.Name}</name></wpt>{Environment.NewLine}";
        }

        text += "<trk>" + Environment.NewLine;
        text += "<name>Листопад 2019</name>" + Environment.NewLine;

        text += "<trkseg>" + Environment.NewLine;

        foreach ( var city in route )
        {
            text += $@"<trkpt lat=""{city.x}"" lon=""{city.y}""></trkpt>{Environment.NewLine}";
        }

        text += "</trkseg>" + Environment.NewLine;
        text += "</trk>" + Environment.NewLine;
        text += "</gpx>" + Environment.NewLine;

        try
        {
            var name = Path.Combine( AssemblyDirectory, Path.GetFileNameWithoutExtension( task ) ) + $"-{tour.Distance:F1}km.gpx";

            File.WriteAllText( name, text, Encoding.UTF8 );
        }
        catch {}        
    }
}

public static class Env
{
    // MutRate should be a low value (1%-3%). How likely a single gene is to be mutated.
    // Every individual is tried for mutation a number of times equal to its number of genes.
    public static double MutRate = 0.025;

    // Elitism a good value is the ceiling of popSize / 10. How many of the best individuals 
    // from the current population should be saved to the next.
    public static int Elitism = 5;

    // PopSize can be any value. Beware that after a certain point too big a population doesn't 
    // make the algorithm find better solutions any faster, and will instead be detrimental.
    // Generally speaking a decent value is 50-70.
    public static int PopSize = 20;
}
