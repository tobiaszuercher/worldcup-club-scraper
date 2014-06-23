using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using ScrapySharp.Extensions;
using ScrapySharp.Network;
using ServiceStack;

namespace WorldCupScraper
{
    class Program
    {
        static void Main(string[] args)
        {
            var urlRegex = new Regex(@"\/(?<name>.*)\/spielplan\/verein\/(?<id>\d+)\/saison_id\/(?<year>\w+)");
            
            var browser = new ScrapingBrowser();
            var home = browser.NavigateToPage(new Uri("http://www.transfermarkt.de/wettbewerbe/fifa"));

            var playerClubMapping = new List<Player>();

            // find all cups
            foreach (var cup in home.Html.CssSelect("a[title~=Weltmeisterschaft]").Skip(1).ToList())
            {
                var cupYear = cup.Attributes["title"].Value.Replace("Weltmeisterschaft ", string.Empty);
                var cupPage = browser.NavigateToPage(new Uri("http://www.transfermarkt.de" + cup.Attributes["href"].Value));

                Console.WriteLine(cupYear);

                // go through each team
                foreach (var teamLink in cupPage.Html.CssSelect("div.four.columns .hauptlink a[href*=saison_id]"))
                {
                    var match = urlRegex.Match(teamLink.Attributes["href"].Value);
                    var team = teamLink.Attributes["title"].Value;

                    try
                    {
                        var teamPage = browser.NavigateToPage(
                            new Uri(
                                string.Format("http://www.transfermarkt.de/{0}/kader/verein/{1}/saison_id/{2}", 
                                match.Groups["name"].Value, 
                                match.Groups["id"].Value, 
                                match.Groups["year"].Value)));

                        var playerNames = teamPage.Html.CssSelect(".spielprofil_tooltip").Select(p => p.InnerText).ToList();
                        var playerClubs = teamPage.Html.CssSelect("table img[src*=wappen]").Select(p => p.Attributes["title"].Value).ToList();

                        for (int i = 0; i < playerNames.Count(); ++i)
                        {
                            playerClubMapping.Add(new Player() { Name = playerNames[i], Club = playerClubs[i], CupYear = cupYear});
                        }

                        Console.WriteLine("Added {0} players for {1}", playerNames.Count(), team);
                    }
                    catch (WebException e)
                    {
                        if (e.Status == WebExceptionStatus.ProtocolError)
                        {
                            Console.WriteLine("HTTP 500 for " + team);
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
            }

            File.WriteAllText("output.txt", playerClubMapping.ToCsv());
        }
    }

    public class Player
    {
        public string Name { get; set; }
        public string Club { get; set; }
        public string CupYear { get; set; }
    }
}
