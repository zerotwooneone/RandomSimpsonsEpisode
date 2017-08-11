using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using Newtonsoft.Json;

namespace randomSimpsonsEpisode
{
    class Program
    {
        private static readonly TimeSpan AllowCancelKeypressTimespan = TimeSpan.FromMilliseconds(300);

        static void Main(string[] args)
        {
            var prog = new Program();

            var random = prog.GetRandom();

            // Setup the configuration to support document loading
            var config = Configuration.Default.WithDefaultLoader();

            var episodes = prog.GetEpisodes(config);

            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
            {
                _keepRunning = false;
            };

            while (_keepRunning)
            {
                var episode = prog.GetRandomEpisode(random, episodes);

                prog.LaunchBrowser(episode.Url);

                prog.Write("Press enter to load another or [CTRL] + [C] to quit");
                Console.ReadLine();
                Thread.Sleep(AllowCancelKeypressTimespan); //this allows the cancel keypress event to fire before we reach the top of the loop

            }


        }

        private void LaunchBrowser(string url)
        {
            Write($"Loading: {url}");
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
            _invalidPathChars = Path.GetInvalidPathChars();
            SeedPath = Path.Combine(AppPath, "random.seed");
            EpisodesPath = Path.Combine(AppPath, "episodes.json");

            if (!Directory.Exists(AppPath))
            {
                Directory.CreateDirectory(AppPath);
            }
        }

        private IEnumerable<Episode> GetEpisodes(IConfiguration configuration)
        {
            var episodesFile = new FileInfo(EpisodesPath);
            var episodes = GetEpisodesFromFile(episodesFile);
            if (episodes == null)
            {
                Write("Cache not found. Building cache...");
                var seasons = GetSeasonsFromOnline(configuration);
                episodes = seasons
                    .SelectMany(s => s.Episodes ?? GetEpisodesFromOnline(configuration, s.Url))
                    .ToArray();
                WriteEpisodesFile(episodes, episodesFile);
            }
            return episodes;
        }

        private void WriteEpisodesFile(IEnumerable<Episode> episodes, FileInfo seasonFile)
        {
            try
            {
                var array = episodes.ToArray();
                var episodesJson = JsonConvert.SerializeObject(array);
                using (var streamWriter = seasonFile.CreateText())
                {
                    streamWriter.WriteLine(episodesJson);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private IEnumerable<Episode> GetEpisodesFromFile(FileInfo episodesFile)
        {
            Write("Looking for cached episodes");
            episodesFile.Refresh();
            if (!episodesFile.Exists)
            {
                Write($"Episodes not found at: {episodesFile.FullName}");
                return null;
            }
            Write($"Cache found: {episodesFile.LastWriteTime}");
            using (var streamReader = episodesFile.OpenText())
            {
                var text = streamReader.ReadToEnd();
                var episodes = JsonConvert.DeserializeObject<Episode[]>(text);
                return episodes;
            }
        }

        private IEnumerable<Episode> GetEpisodesFromOnline(IConfiguration configuration, string seasonUrl)
        {
            Write("Getting one season's episodes from online...");
            // This CSS selector gets the desired content
            var cellSelector = "ul > li > a[title^='Watch']";
            // Perform the query to get all cells with the content
            IHtmlCollection<IElement> cells = GetSeasonDocument(configuration, seasonUrl).Result.QuerySelectorAll(cellSelector);
            // We are only interested in the text - select it with LINQ
            var episodes = cells.Select(m => new Episode { Text = m.TextContent, Url = m.GetAttribute("href") });

            //Write(string.Join(Environment.NewLine, episodes.Select(s => s.Text)));
            return episodes;
        }

        private IEnumerable<Season> GetSeasonsFromOnline(IConfiguration config)
        {
            Write("Getting seasons from online...");
            // This CSS selector gets the desired content
            const string cellSelector = "ul > li a[title^='The Simpsons']";
            // Perform the query to get all cells with the content
            IHtmlCollection<IElement> cells = GetSeasonDocument(config, "https://www.watchcartoononline.io/cartoon-list").Result.QuerySelectorAll(cellSelector);
            // We are only interested in the text - select it with LINQ
            var seasons = cells.SelectMany(m =>
            {
                var href = m.GetAttribute("href");
                return m.TextContent == "The Simpsons" ? GetSeasonsFromOnlineSpecialCase(config, href) :
                new[] { new Season { Text = m.TextContent, Url = href } };
            });

            return seasons;
        }

        private IEnumerable<Season> GetSeasonsFromOnlineSpecialCase(IConfiguration config, string parentUrl)
        {
            const string cellSelector = "ul > li > a[title^='Watch The Simpsons']";
            IHtmlCollection<IElement> cells = GetSeasonDocument(config, parentUrl).Result
                .QuerySelectorAll(cellSelector);
            var seasons = new Dictionary<string, Season>();
            foreach (var a in cells)
            {
                string textContent = a.TextContent;
                if (textContent == "The Simpsons Movie") continue;

                const string seasonEpisodePattern =
                    @"The Simpsons Season ([0-9]+) Episode ([0-9]+) – (.*)";
                var match = Regex.Match(textContent, seasonEpisodePattern);
                string seasonNumberText;
                string episodeNumberText;
                string episodeName;
                if (match.Success)
                {
                    seasonNumberText = match.Groups[1].Value;
                    episodeNumberText = match.Groups[2].Value;
                    episodeName = match.Groups[3].Value;
                }
                else
                {
                    const string concatSeasonEpisodePattern = @"The Simpsons Episode ([0-9])([0-9][0-9]) (.*)";
                    match = Regex.Match(textContent, concatSeasonEpisodePattern);
                    if (match.Success)
                    {
                        seasonNumberText = match.Groups[1].Value;
                        episodeNumberText = match.Groups[2].Value;
                        episodeName = match.Groups[3].Value;
                    }
                    else
                    {
                        throw new Exception("Unexpected episode format.");
                    }
                }
                var href = a.GetAttribute("href");
                var episode = new Episode { Text = episodeName, Url = href };
                Season season;
                if (seasons.ContainsKey(seasonNumberText))
                {
                    season = seasons[seasonNumberText];
                    var episodes = season.Episodes.ToList();
                    episodes.Add(episode);
                    season.Episodes = episodes;
                }
                else
                {
                    season = new Season { Text = $"The Simpsons Season {seasonNumberText}", Episodes = new[] { episode } };
                    seasons[seasonNumberText] = season;
                }
            }
            return seasons.Values;
        }

        private void Write(string message)
        {
            Console.WriteLine(message);
        }



        public string EpisodesPath { get; }

        public string SeedPath { get; }

        public string AppPath { get; }

        private readonly char[] _invalidPathChars;
        private static volatile bool _keepRunning = true;

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

        /// <summary>
        /// This will be null for seasons (6+) which do not have their own url.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// This will be null for seasons (1-6) which we have to visit the url to get episodes.
        /// </summary>
        public IEnumerable<Episode> Episodes { get; set; }
    }

    public class Episode
    {
        public string Text { get; set; }
        public string Url { get; set; }
    }
}
