using BooruSharp.Booru;
using BooruSharp.Search;
using BooruSharp.Search.Tag;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
            var entryAnime = "kancolle";

            Dictionary<string, Dictionary<string, List<Entry>>> allEntries = new Dictionary<string, Dictionary<string, List<Entry>>>();
            List<int> alreadyAsked = new List<int>(); // Ids already asked

            var booru = new Gelbooru();

            var result = await booru.GetRandomPostsAsync(int.MaxValue, new[] { "yuri", "2girls", entryCharacter });

            Dictionary<string, bool> characters = new Dictionary<string, bool>(); // Is a tag a character

            foreach (var r in result)
            {
                if (alreadyAsked.Contains(r.id)) // We already parsed this image
                    continue;

                alreadyAsked.Add(r.id);

                List<string> imageCharacs = new List<string>();

                foreach (var t in r.tags)
                {
                    if (characters.ContainsKey(t)) // TODO: Manage series
                    {
                        if (characters[t])
                            imageCharacs.Add(t);
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

                    characters.Add(t, tagRes.type == TagType.Character);
                    if (tagRes.type == TagType.Character)
                        imageCharacs.Add(t);
                }

                if (imageCharacs.Count == 2)
                {
                    imageCharacs.OrderBy(x => x);
                    string c1 = Regex.Replace(imageCharacs[0], "\\([^\\)]+\\)", "").Replace('_', ' ').Trim();
                    string c2 = Regex.Replace(imageCharacs[1], "\\([^\\)]+\\)", "").Replace('_', ' ').Trim();

                    if (!allEntries.ContainsKey(c1))
                        allEntries.Add(c1, new Dictionary<string, List<Entry>>());

                    if (!allEntries[c1].ContainsKey(c2))
                        allEntries[c1].Add(c2, new List<Entry>());

                    if (allEntries[c1][c2].Count < 2)
                        allEntries[c1][c2].Add(
                            new Entry
                            {
                                link = r.postUrl.AbsoluteUri,
                                imageId = r.fileUrl.AbsoluteUri,
                                linkType = "gelbooru",
                                nsfw = (int)r.rating
                            });

                    Console.WriteLine($"Found relation between {c1} and {c2}.");
                }
            }

            File.WriteAllText(entryAnime + ".json", JsonConvert.SerializeObject(new Json
            {
                color = "lightblue",
                name = entryAnime,
                ships = allEntries
            }));

            Console.WriteLine("JSON saved\n\n");

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
