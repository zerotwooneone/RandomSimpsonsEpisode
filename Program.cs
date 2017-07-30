using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;

namespace randomSimpsonsEpisode
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            var prog = new Program();

            var random = prog.GetRandom();

            // Setup the configuration to support document loading
            var config = Configuration.Default.WithDefaultLoader();

            var seasons = prog.GetSeasons(config);
            var season = prog.GetRandomSeason(random, seasons);

            var episodes = prog.GetEpisodes(config, season);
            var episode = prog.GetRandomEpisode(random, episodes);

            prog.LaunchBrowser(episode.Url);

        }

        private void LaunchBrowser(string url)
        {
            System.Diagnostics.Process.Start("cmd", $"/C start {url}"); //this is windows-only, needs work
        }

        private Episode GetRandomEpisode(Random random, IEnumerable<Episode> episodes)
        {
            var episodeArray = episodes.ToArray();
            var index = random.Next(episodeArray.Length);
            return episodeArray[index];
        }

        public Program()
        {
            AppPath = Path.Combine(Environment.GetEnvironmentVariable("LocalAppData"), "RandomSimpsonsEpisode");
            SeedPath = Path.Combine(AppPath, "random.seed");
            SeasonsPath = Path.Combine(AppPath, "seasons.json");
        }

        private IEnumerable<Episode> GetEpisodes(IConfiguration configuration, Season season)
        {
            var episodes = GetEpisodesFromOnline(configuration, season.Url);
            return episodes;
        }

        private IEnumerable<Episode> GetEpisodesFromOnline(IConfiguration configuration, string seasonUrl)
        {
            // This CSS selector gets the desired content
            var cellSelector = "ul > li > a[title^='Watch']";
            // Perform the query to get all cells with the content
            IHtmlCollection<IElement> cells = GetSeasonDocument(configuration, seasonUrl).Result.QuerySelectorAll(cellSelector);
            // We are only interested in the text - select it with LINQ
            var episodes = cells.Select(m => new Episode { Text = m.TextContent, Url = m.GetAttribute("href") });

            Write(string.Join(Environment.NewLine, episodes.Select(s => s.Text)));
            return episodes;
        }

        private Season GetRandomSeason(Random random, IEnumerable<Season> seasons)
        {
            var seasonArray = seasons.ToArray();
            var index = random.Next(seasonArray.Length);
            return seasonArray[index];
        }

        private IEnumerable<Season> GetSeasons(IConfiguration config)
        {
            var seasons = GetSeasonsFromFile();

            if (seasons == null)
            {
                seasons = GetSeasonsFromOnline(config);
            }
            return seasons;
        }

        private IEnumerable<Season> GetSeasonsFromOnline(IConfiguration config)
        {
            // This CSS selector gets the desired content
            var cellSelector = "ul > li a[title^='The Simpsons']";
            // Perform the query to get all cells with the content
            IHtmlCollection<IElement> cells = GetSeasonDocument(config, "https://www.watchcartoononline.io/cartoon-list").Result.QuerySelectorAll(cellSelector);
            // We are only interested in the text - select it with LINQ
            var seasons = cells.Select(m => new Season { Text = m.TextContent, Url = m.GetAttribute("href")});

            Write(string.Join(Environment.NewLine, seasons.Select(s=>s.Text)));
            return seasons;
        }

        private IEnumerable<Season> GetSeasonsFromFile()
        {
            Write("Looking for cached seasons");
            var seasonsFile = new FileInfo(SeasonsPath);

            seasonsFile.Refresh(); //not required, but good practice
            if (!seasonsFile.Exists)
            {
                return null;
            }
            Write($"Cache found :{seasonsFile.LastWriteTime}");
            return null;
        }

        private void Write(string message)
        {
            Console.WriteLine(message);
        }

        

        public string SeasonsPath { get; }

        public string SeedPath { get; }

        public string AppPath { get; }

        public Random GetRandom()
        {
            try
            {
                var seedText = File.ReadAllText(SeedPath);
                var seed = int.Parse(seedText);
                return new Random(seed);
            }
            catch (Exception)
            {
                Console.WriteLine("Using random");
            }
            return new Random();
        }

        public async Task<IDocument> GetSeasonDocument(IConfiguration configuration, string url)
        {
            // Asynchronously get the document in a new context using the configuration
            var document = await BrowsingContext.New(configuration).OpenAsync(url);

            return document;
        }
    }

    public class Season
    {
        public string Text { get; set; }
        public string Url { get; set; }
    }

    public class Episode
    {
        public string Text { get; set; }
        public string Url { get; set; }
    }
}
