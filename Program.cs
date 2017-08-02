﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using Newtonsoft.Json;

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
            _invalidPathChars = Path.GetInvalidPathChars();
            SeedPath = Path.Combine(AppPath, "random.seed");
            SeasonsPath = Path.Combine(AppPath, "seasons.json");

            if (!Directory.Exists(AppPath))
            {
                Directory.CreateDirectory(AppPath);
            }
        }

        private IEnumerable<Episode> GetEpisodes(IConfiguration configuration, Season season)
        {
            var seasonFilePath = GetSeasonFilePath(season.Text); 
            var seasonFile = new FileInfo(seasonFilePath);
            var episodes = GetEpisodesFromFile(seasonFile);
            if (episodes == null)
            {
                episodes = GetEpisodesFromOnline(configuration, season.Url);
                WriteSeasonFile(episodes, seasonFile);
            }
            return episodes;
        }

        private void WriteSeasonFile(IEnumerable<Episode> episodes, FileInfo seasonFile)
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

        private IEnumerable<Episode> GetEpisodesFromFile(FileInfo seasonFile)
        {
            Write("Looking for cached season");
            seasonFile.Refresh();
            if (!seasonFile.Exists)
            {
                return null;
            }
            Write($"Cache found: {seasonFile.LastWriteTime}");
            using (var streamReader = seasonFile.OpenText())
            {
                var text = streamReader.ReadToEnd();
                var episodes = JsonConvert.DeserializeObject<Episode[]>(text);
                return episodes;
            }
        }

        private string GetSeasonFilePath(string seasonText)
        {
            var splitText = seasonText.Split(_invalidPathChars, StringSplitOptions.RemoveEmptyEntries);
            var newName = String.Join("_", splitText).TrimEnd('.');
            return Path.Combine(AppPath, newName);
        }
        
        private IEnumerable<Episode> GetEpisodesFromOnline(IConfiguration configuration, string seasonUrl)
        {
            // This CSS selector gets the desired content
            var cellSelector = "ul > li > a[title^='Watch']";
            // Perform the query to get all cells with the content
            IHtmlCollection<IElement> cells = GetSeasonDocument(configuration, seasonUrl).Result.QuerySelectorAll(cellSelector);
            // We are only interested in the text - select it with LINQ
            var episodes = cells.Select(m => new Episode { Text = m.TextContent, Url = m.GetAttribute("href") });

            //Write(string.Join(Environment.NewLine, episodes.Select(s => s.Text)));
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
            var seasonsFile = new FileInfo(SeasonsPath);
            var seasons = GetSeasonsFromFile(seasonsFile);

            if (seasons == null)
            {
                seasons = GetSeasonsFromOnline(config);
                WriteSeasonsFile(seasons, seasonsFile);
            }
            return seasons;
        }

        private void WriteSeasonsFile(IEnumerable<Season> seasons, FileInfo seasonsFile)
        {
            try
            {
                var array = seasons.ToArray();

                var seasonJson = JsonConvert.SerializeObject(array);

                using (var streamWriter = seasonsFile.CreateText())
                {
                    streamWriter.Write(seasonJson);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private IEnumerable<Season> GetSeasonsFromOnline(IConfiguration config)
        {
            // This CSS selector gets the desired content
            var cellSelector = "ul > li a[title^='The Simpsons']";
            // Perform the query to get all cells with the content
            IHtmlCollection<IElement> cells = GetSeasonDocument(config, "https://www.watchcartoononline.io/cartoon-list").Result.QuerySelectorAll(cellSelector);
            // We are only interested in the text - select it with LINQ
            var seasons = cells.Select(m => new Season { Text = m.TextContent, Url = m.GetAttribute("href") });

            //Write(string.Join(Environment.NewLine, seasons.Select(s => s.Text)));
            return seasons;
        }

        private IEnumerable<Season> GetSeasonsFromFile(FileInfo seasonsFile)
        {
            Write("Looking for cached seasons");
            seasonsFile.Refresh();
            if (!seasonsFile.Exists)
            {
                return null;
            }
            Write($"Cache found :{seasonsFile.LastWriteTime}");
            using (var streamReader = seasonsFile.OpenText())
            {
                var text = streamReader.ReadToEnd();
                var seasons = JsonConvert.DeserializeObject<Season[]>(text);
                return seasons;
            }
        }

        private void Write(string message)
        {
            Console.WriteLine(message);
        }



        public string SeasonsPath { get; }

        public string SeedPath { get; }

        public string AppPath { get; }

        private readonly char[] _invalidPathChars;

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
