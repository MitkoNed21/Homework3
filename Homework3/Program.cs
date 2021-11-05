using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Homework3
{
    class Program
    {
        static void Main(string[] args)
        {
            string url;
            var showedInitialMessage = false;

            while (true)
            {
                if (!showedInitialMessage)
                {
                    Console.WriteLine("Please enter an Amazon products list url (preceded with http:// or https://).");
                    showedInitialMessage = true;
                }

                url = Console.ReadLine();

                // from https://stackoverlow.com/questions/7578857
                // answer by Arabela Paslaru, 28.09.2011, 10:30
                // edited by Jon Schneider, 12.11.2015, 21:58
                // link to answer: https://stackoverflow.com/a/7581824
                // https://creativecommons.org/licenses/by-sa/3.0/
                // and comments by 41686d6564 on answer by 41686d6564, 12.10.2012, 5:37
                // link to answer: https://stackoverflow.com/a/52772923
                // https://creativecommons.org/licenses/by-sa/4.0/
                if (!(url.Contains('.') && 
                    Uri.TryCreate(url, UriKind.Absolute, out Uri _outUri) &&
                    (_outUri.Scheme == "http" || _outUri.Scheme == "https") &&
                    _outUri.IsWellFormedOriginalString()))
                {
                    Console.WriteLine("Please enter a valid url!");
                }
                else
                {
                    var tempUrl = url.Substring(url.IndexOf("//") + 2);
                    if (!(tempUrl.StartsWith("www.amazon.com") ||
                          tempUrl.StartsWith("amazon.com")))
                    {
                        Console.WriteLine("Please enter a url from amazon.com!");
                    }
                    else
                    {
                        break;
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("Retreiving info, please wait...");
            Console.WriteLine();
            using var webClient2 = new WebClient();

            webClient2.Headers.Add(HttpRequestHeader.ContentType, "text/html; charset=UTF-8");

            var htmlContent = webClient2.DownloadString(url);
            var finalContent = MakeContentSmaller(htmlContent);

            var productsLinks = GetProductLinksFromPage(finalContent);

            var taskList = new List<Task<string>>();

            foreach (var prLink in productsLinks)
            {
                taskList.Add(new Task<string>(() =>
                {
                    using var webClient = new WebClient();
                    webClient.Headers.Add(HttpRequestHeader.ContentType, "text/html; charset=utf-8");
                    var content = webClient.DownloadString("https://www.amazon.com" + prLink);

                    var content1 = MakeContentSmaller(content);
                    var productInfo = Task.Run(() => GetFromProductInfoPage(content1));

                    return String.Join(
                        $"\n{new String('-', 40)}\n",
                        productInfo.Result.Select(
                            kvp => $"{kvp.Key}: {kvp.Value}"
                        )
                    );
                }));
            }

            foreach (var task in taskList)
            {
                task.Start();
            }

            var fanInTask = Task.WhenAll(taskList);

            var finalTask = fanInTask.ContinueWith(t =>
            {
                Console.WriteLine(String.Join($"\n{new String('=', 40)}\n", t.Result));
                Console.WriteLine();
            });
            finalTask.Wait();

            Console.Write("Press any key to exit.");
            Console.ReadKey();
        }

        private static string MakeContentSmaller(string htmlContent)
        {
            var smallerContent = Regex.Replace(
                            Regex.Replace(
                                htmlContent,
                                pattern: @"<script.+?</script>|<!--.+?-->|<style.+?</style>|<noscript.+?</noscript>",
                                replacement: "",
                                RegexOptions.Singleline
                            ),
                            pattern: @"(?:\n|\r\n)+",
                            replacement: "\n"
                        );

            var finalContent = smallerContent
                .Replace("&quot;", "\"")
                .Replace("&lrm;", "")
                .Replace("&rlm;", "")
                .Replace("&amp;", "&");
            return finalContent;
        }

        private static List<string> GetProductLinksFromPage(string htmlContent)
        {
            var result = new List<string>();

            var productNameMatch = Regex.Matches(
                htmlContent,
                @"<a.+?href=""(.+?)"">.+?<span class="".+?a-color-base a-text-normal"">.+?</span>"
            );

            for (int i = 0; i < productNameMatch.Count; i++)
            {
                Match match = productNameMatch[i];
                if (match.Success)
                {
                    result.Add(match.Groups.Values.Skip(1).First().Value);
                }
            }

            return result;
        }

        private static Dictionary<string, string> GetFromProductInfoPage(string htmlContent)
        {
            var result = new Dictionary<string, string>();

            var productNameMatch = Regex.Match(
                htmlContent,
                @"<span id=""productTitle"".+?>(.+?)</span>",
                RegexOptions.Singleline
            );
            var productName = "";

            if (productNameMatch.Success)
            {
                productName = productNameMatch.Groups.Values.Skip(1).First().Value.Trim();
                //productName = productNameMatch.Value.Trim();
            }

            var priceRangeMatch = Regex.Match(
                htmlContent,
                @"<span class=""a-price-range"">.+?<span class=""a-offscreen"">(.+?)</span>.+?<span class=""a-offscreen"">(.+?)</span>",
              //@">Price:</td>.+?<span class=""a-price-range"">.+?<span class=""a-offscreen"">(.+?)</span>.+?<span class=""a-offscreen"">(.+?)</span>"
                RegexOptions.Singleline
            );

            var priceMatch = Regex.Match(
                htmlContent,
                @">Price:</td>.+?<span.+?>(\$.+?)</span>",
                RegexOptions.Singleline
            );

            var price = "";
            if (priceRangeMatch.Success)
            {
                price = priceRangeMatch.Groups.Values.Skip(1).First().Value + "-" +
                        priceRangeMatch.Groups.Values.Skip(2).First().Value;
            }
            else if (priceMatch.Success)
            {
                price = priceMatch.Groups.Values.Skip(1).First().Value;
            }
            else
            {
                price = "-";
            }

            var ratingMatch = Regex.Match(
                htmlContent,
                @"<i class=""a-icon a-icon-star a-star-[0-9](?:-[0-9])?""><span class=""a-icon-alt"">(.+)</span></i>"
            );

            var rating = "";
            if (ratingMatch.Success)
            {
                rating = ratingMatch.Groups.Values.Skip(1).First().Value;
            }
            else
            {
                rating = "-";
            }

            var productInfoMatches = Regex.Matches(
                htmlContent,
  @"<tr>\n<th class=""a-color-secondary a-size-base prodDetSectionEntry"">\n(.+)\n</th>\n<td class=""a-size-base prodDetAttrValue"">\n(.+)\n</td>"
            );

            var productInfo = new Dictionary<string, string>();

            foreach (Match match in productInfoMatches)
            {
                if (match.Success)
                {
                    var attr = match.Groups.Values.Skip(1).First().Value.Trim();
                    var val = match.Groups.Values.Skip(2).First().Value.Trim();

                    if (!productInfo.ContainsKey(attr))
                    {
                        productInfo[attr] = val;
                    }
                }
            }

            result.Add("Product Name", productName);
            result.Add("Price", price);
            result.Add("Rating", rating);
            result.Add("Product info", String.Join("\n  ", productInfo.Select(
                kvp => $"{kvp.Key}: {kvp.Value}"
            )));

            return result;
        }
    }
}
