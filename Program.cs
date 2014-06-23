using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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

            var playerClubMapping = new ConcurrentStack<Player>();

            // find all cups
            Parallel.ForEach(home.Html.CssSelect("a[title~=Weltmeisterschaft]").Skip(1), cup =>
            {
                var cupYear = cup.Attributes["title"].Value.Replace("Weltmeisterschaft ", string.Empty);

                var cupPage = browser.NavigateToPage(new Uri("http://www.transfermarkt.de" + cup.Attributes["href"].Value));

                Parallel.ForEach(cupPage.Html.CssSelect("div.four.columns .hauptlink a[href*=saison_id]"), teamLink =>
                {
                    var match = urlRegex.Match(teamLink.Attributes["href"].Value);
                    var team = teamLink.Attributes["title"].Value;

                    try
                    {
                        var teamPage = new ScrapingBrowser().NavigateToPage(
                            new Uri(
                                string.Format("http://www.transfermarkt.de/{0}/kader/verein/{1}/saison_id/{2}",
                                match.Groups["name"].Value,
                                match.Groups["id"].Value,
                                match.Groups["year"].Value)));

                        var playerNames = teamPage.Html.CssSelect(".spielprofil_tooltip").Select(p => p.InnerText).ToList();
                        var playerClubs = teamPage.Html.CssSelect("table img[src*=wappen]").Select(p => p.Attributes["title"].Value).ToList();

                        for (int i = 0; i < playerNames.Count(); ++i)
                        {
                            playerClubMapping.Push(new Player()
                            {
                                Name = playerNames[i].Replace(" †", string.Empty),
                                Club = playerClubs[i], 
                                CupYear = cupYear
                            });
                        }

                        Console.WriteLine("Added {0} players for {1} (World Cup {2})", playerNames.Count(), team, cupYear);
                    }
                    catch (WebException e)
                    {
                        if (e.Status == WebExceptionStatus.ProtocolError)
                        {
                            Console.WriteLine("HTTP 500 for {0} ({1})", team, cupYear);
                        }
                        else
                        {
                            throw;
                        }
                    }
                });
            });

            File.WriteAllText("output.csv", playerClubMapping.ToCsv());
        }
    }

    public class Player
    {
        public string Name { get; set; }
        public string Club { get; set; }
        public string CupYear { get; set; }
    }
}
