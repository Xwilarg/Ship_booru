using BooruSharp.Booru;
using BooruSharp.Search;
using BooruSharp.Search.Tag;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Ship_booru
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var entryCharacter = "ikazuchi_(kantai_collection)"; // First search
            var entryAnime = "kantai_collection";

            Dictionary<string, Json> allEntries = new Dictionary<string, Json>();
            List<int> alreadyAsked = new List<int>(); // Ids already asked
            List<string> charactersAlreadyAsked = new List<string>();

            var booru = new Gelbooru();
            var rand = new Random();

            Dictionary<string, TagType> tags = new Dictionary<string, TagType>();

            List<(string, string)> remainingCharacters = new List<(string, string)>
            {
                (entryCharacter, entryAnime)
            };

            if (Directory.Exists("Result"))
            {
                foreach (var file in Directory.GetFiles("Result"))
                    File.Delete(file);
            }
            else
                Directory.CreateDirectory("Result");

        next:
            var current = remainingCharacters[0];
            charactersAlreadyAsked.Add(current.Item1);
            remainingCharacters.RemoveAt(0);

            var result = await booru.GetRandomPostsAsync(int.MaxValue, new[] { "yuri", "2girls", current.Item1 });

            foreach (var r in result)
            {
                if (alreadyAsked.Contains(r.id)) // We already parsed this image
                    continue;

                alreadyAsked.Add(r.id);

                List<string> licences = new List<string>();
                List<string> characters = new List<string>();

                foreach (var t in r.tags)
                {
                    if (tags.ContainsKey(t)) // TODO: Manage series
                    {
                        var tag = tags[t];
                        if (tag == TagType.Character)
                            characters.Add(t);
                        else if (tag == TagType.Copyright)
                            licences.Add(t);
                        continue;
                    }

                    SearchResult tagRes;

                    try
                    {
                        tagRes = await booru.GetTagAsync(t);
                    }
                    catch (Exception e)
                    {
                        if (e is ArgumentException || e is InvalidTags)
                            continue;
                        throw;
                    }

                    tags.Add(t, tagRes.type);
                    if (tagRes.type == TagType.Character)
                        characters.Add(t);
                    else if (tagRes.type == TagType.Copyright)
                        licences.Add(t);
                }

                if (characters.Any(x => x.Contains("admiral"))) // TODO: Put that elsewhere
                    continue;

                if (characters.Count == 2 && licences.Count <= 2 && characters.Contains(current.Item1) && licences.Contains(current.Item2))
                {
                    characters.OrderBy(x => x);

                    var otherLicence = licences.Where(x => x != current.Item2).FirstOrDefault()?.Replace("_", ""); // Will be null if it's not a crossover

                    string licenceName = otherLicence == null ? current.Item2.Replace("_", "") : "crossover";

                    string c1, c2;

                    if (otherLicence != null) // Crossover
                    {
                        var a = Regex.Replace(characters[0], "\\([^\\)]+\\)", "").Replace('_', ' ').Trim();
                        var b = Regex.Replace(characters[1], "\\([^\\)]+\\)", "").Replace('_', ' ').Trim();

                        if (a == current.Item1)
                        {
                            a = current.Item2 + "_" + a;
                            b = otherLicence + "_" + b;
                        }
                        else
                        {
                            b = current.Item2 + "_" + b;
                            a = otherLicence + "_" + a;
                        }

                        c1 = string.Compare(a, b) < 0 ? a : b;
                        c2 = string.Compare(a, b) < 0 ? b : a;
                    }
                    else
                    {
                        c1 = Regex.Replace(characters[0], "\\([^\\)]+\\)", "").Replace('_', ' ').Trim();
                        c2 = Regex.Replace(characters[1], "\\([^\\)]+\\)", "").Replace('_', ' ').Trim();
                    }

                    if (!allEntries.ContainsKey(licenceName))
                        allEntries.Add(licenceName, new Json()
                        {
                            name = licenceName,
                            ships = new Dictionary<string, Dictionary<string, List<Entry>>>(),
                            color = $"#{rand.Next(127, 256).ToString("x2")}{rand.Next(127, 256).ToString("x2")}{rand.Next(127, 256).ToString("x2")}"
                        });

                    if (!allEntries[licenceName].ships.ContainsKey(c1))
                        allEntries[licenceName].ships.Add(c1, new Dictionary<string, List<Entry>>());

                    if (!allEntries[licenceName].ships[c1].ContainsKey(c2))
                        allEntries[licenceName].ships[c1].Add(c2, new List<Entry>());

                    if (allEntries[licenceName].ships[c1][c2].Count < 3)
                        allEntries[licenceName].ships[c1][c2].Add(
                            new Entry
                            {
                                link = r.postUrl.AbsoluteUri,
                                imageId = r.fileUrl.AbsoluteUri,
                                linkType = "gelbooru",
                                nsfw = (int)r.rating
                            });

                    foreach (var s in characters)
                    {
                        if (!charactersAlreadyAsked.Contains(s))
                        {
                            charactersAlreadyAsked.Add(s);
                            remainingCharacters.Add((s, otherLicence ?? licences[0]));
                        }
                    }

                    Console.WriteLine($"Found relation between {c1} and {c2}. ({licenceName})");
                }
            }

            foreach (var j in allEntries)
            {
                File.WriteAllText("Result/" + j.Key + ".json", JsonConvert.SerializeObject(j.Value));
            }

            var d = new Dictionary<string, string[]>();
            d.Add("names", allEntries.Select(x => x.Key).Where(x => x != "crossover").ToArray());

            File.WriteAllText("Result/names.json", JsonConvert.SerializeObject(d));

            Console.WriteLine("JSON saved\n\n");

            if (remainingCharacters.Count > 0)
                goto next;

            Console.WriteLine("Press any key to continue");
            Console.ReadKey();
        }
    }

    public struct Json
    {
        public string name;
        public string color;
        public Dictionary<string, Dictionary<string, List<Entry>>> ships;
    }

    public struct Entry
    {
        public int nsfw;
        public string imageId;
        public string linkType;
        public string link;
    }
}
