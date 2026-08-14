using EnglishLeitner.OxfordWebScraper.DTOs;
using HtmlAgilityPack;

namespace EnglishLeitner.OxfordWebScraper;

public class Scraper
{
    private const string BaseUrl = "https://www.oxfordlearnersdictionaries.com";
    private const string WorldListUrl = $"{BaseUrl}/wordlists/oxford3000-5000";

    public static async Task<string[]> GetWordPathsAsync(CancellationToken cancellationToken = default)
    {
        using HttpClient client = new();
        Stream htmlStream = await client.GetStreamAsync(WorldListUrl, cancellationToken);

        HtmlDocument doc = new();
        doc.Load(htmlStream);

        var linkNodes = doc.DocumentNode.SelectNodes(
            "//*[contains(concat(' ', normalize-space(@class), ' '), ' top-g ')]//li//a[@href]"
        );

        return linkNodes?
            .Select(a => a.GetAttributeValue("href", string.Empty))
            .Where(href => !string.IsNullOrWhiteSpace(href))
            .Distinct()
            .ToArray()
            ?? [];
    }

    public static async Task<string> ScrapHtmlAsync(string path, CancellationToken cancellationToken = default)
    {
        Uri? uri = new(new Uri(BaseUrl), path);
        using HttpClient client = new();
        client.Timeout = TimeSpan.FromSeconds(5);
        using HttpResponseMessage response = await client.GetAsync(uri, cancellationToken = default);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public static Word Parse(Stream htmlStream, string url)
    {
        Word word = new()
        {
            WebPage = new Uri(new Uri(BaseUrl), relativeUri: url).AbsoluteUri
        };

        HtmlDocument doc = new();
        doc.Load(htmlStream);

        HtmlNode? content = doc.DocumentNode.SelectSingleNode(@"//*[@id=""entryContent""]");

        HtmlNode? topContainer = content?.SelectSingleNode(@".//*[contains(concat(' ', normalize-space(@class), ' '), 'top-container')]");

        HtmlNode? headWord = topContainer?.SelectSingleNode(@".//h1");
        string? headWordValue = headWord?.InnerText;
        word.HeadWord = headWordValue?.Trim();

        HtmlNode? position = topContainer?.SelectSingleNode(@".//*[contains(concat(' ', normalize-space(@class), ' '), 'pos')]");
        string? positionValue = position?.InnerText;
        word.Position = positionValue?.Trim();

        HtmlNode? headGrammar = topContainer?.SelectSingleNode(@".//*[contains(concat(' ', normalize-space(@class), ' '), 'grammar')]");
        string? headGrammarValue = headGrammar?.InnerText;
        word.Grammar = headGrammarValue?.Trim();

        word.Pronunciations = [];
        HtmlNode? phonetics = topContainer?.SelectSingleNode(@".//*[contains(concat(' ', normalize-space(@class), ' '), 'phonetics')]");

        foreach (string lang in new string[] { "phons_br", "phons_n_am" })
        {
            HtmlNode? langElem = phonetics?.SelectSingleNode($@".//*[contains(concat(' ', normalize-space(@class), ' '), '{lang}')]");

            HtmlNodeCollection? audios = langElem?.SelectNodes(@".//*[@data-src-mp3 or @data-src-ogg]");
            if (audios?.Count > 0)
            {
                foreach (var audio in audios)
                {
                    string? mp3Url = audio?.GetDataAttribute("src-mp3").Value;
                    string? oggUrl = audio?.GetDataAttribute("src-ogg").Value;

                    HtmlNode? phon = audio?.NextSibling.HasClass("phon") == true ? audio?.NextSibling : null;
                    string? phonValue = phon?.InnerText;

                    word.Pronunciations.Add(new Pronunciation()
                    {
                        IsUK = lang == "phons_br",
                        Phonetics = phonValue?.Trim(),
                        Mp3Url = mp3Url?.Trim(),
                        OggUrl = oggUrl?.Trim(),
                    });
                }
            }
        }

        HtmlNode? headCefr = topContainer?.SelectSingleNode(@".//*[contains(concat(' ', normalize-space(@class), ' '), 'symbols')]//a");
        string? headCefrHref = headCefr?.GetAttributeValue<string?>("href", null);
        headCefrHref = headCefrHref?.Replace("&amp;", "&");
        Uri? cefrUrl = string.IsNullOrWhiteSpace(headCefrHref) ? null : new(headCefrHref);
        string? headCefrValue = cefrUrl is not null ? System.Web.HttpUtility.ParseQueryString(cefrUrl.Query).Get("level") : null;

        word.Cefr = headCefrValue?.Trim().ToUpper() switch
        {
            "A1" => (int)CefrLevel.A1,
            "A2" => (int)CefrLevel.A2,
            "B1" => (int)CefrLevel.B1,
            "B2" => (int)CefrLevel.B1,
            "C1" => (int)CefrLevel.C1,
            "C2" => (int)CefrLevel.C2,
            _ => null
        };

        word.Meanings = [];
        // ol.senses_multiple
        HtmlNode? groups = content?.SelectSingleNode(@".//*[contains(concat(' ', normalize-space(@class), ' '), 'senses_multiple')]");

        // ol.senses_multiple > .shcut-g:has(> h2 + li)
        HtmlNodeCollection? groupCollection = groups?.SelectNodes(@"./*[contains(concat(' ', normalize-space(@class), ' '), 'shcut-g')]");

        // ol.senses_multiple:has(> li)
        groupCollection ??= content?.SelectNodes(@".//*[contains(concat(' ', normalize-space(@class), ' '), 'senses_multiple')]");

        // ol.sense_single:has(> li)
        groupCollection ??= content?.SelectNodes(@".//*[contains(concat(' ', normalize-space(@class), ' '), 'sense_single')]");
        if (groupCollection?.Count > 0)
        {
            foreach (HtmlNode group in groupCollection)
            {
                MeaningGroup meaningGroup = new();
                word.Meanings.Add(meaningGroup);

                HtmlNode? groupHead = group.SelectSingleNode(@".//h2");
                string? groupHeadValue = groupHead?.InnerText;
                meaningGroup.Head = groupHeadValue?.Trim();

                meaningGroup.Meanings = [];
                HtmlNodeCollection? groupItems = group.SelectNodes(@".//li[contains(concat(' ', normalize-space(@class), ' '), 'sense')]");
                if (groupItems?.Count > 0)
                {
                    foreach (HtmlNode sense in groupItems)
                    {
                        MeaningItem meaning = new();
                        meaningGroup.Meanings.Add(meaning);

                        int? senseNum = sense.GetAttributeValue<int?>("sensenum", null);
                        meaning.Number = senseNum;

                        string? cefrLevelValue = sense.GetAttributeValue<string?>("cefr", null);
                        cefrLevelValue ??= sense.GetAttributeValue<string?>("fkcefr", null);
                        meaning.Cefr = cefrLevelValue?.Trim().ToUpper() switch
                        {
                            "A1" => (int)CefrLevel.A1,
                            "A2" => (int)CefrLevel.A2,
                            "B1" => (int)CefrLevel.B1,
                            "B2" => (int)CefrLevel.B1,
                            "C1" => (int)CefrLevel.C1,
                            "C2" => (int)CefrLevel.C2,
                            _ => null
                        };

                        HtmlNode? grammar = sense.SelectSingleNode(@".//*[contains(concat(' ', normalize-space(@class), ' '), 'grammar')]");
                        string? grammarValue = grammar?.InnerText;
                        meaning.Grammar = grammarValue?.Trim();

                        HtmlNode? def = sense.SelectSingleNode(@".//*[contains(concat(' ', normalize-space(@class), ' '), 'def')]");
                        string? defValue = def?.InnerText;
                        meaning.Definition = defValue?.Trim();

                        HtmlNode? variants = sense.SelectSingleNode(@".//*[contains(concat(' ', normalize-space(@class), ' '), 'variants')]");
                        string? variantsValue = variants?.InnerText;
                        meaning.Variants = variantsValue?.Trim();

                        HtmlNodeCollection? refs = sense.SelectNodes(@".//*[contains(concat(' ', normalize-space(@class), ' '), 'xrefs')]");
                        List<string> refValues = refs?.Select(x => x.InnerText.Trim()).Where(x => x != string.Empty)?.ToList() ?? [];
                        meaning.Refs = refValues;

                        HtmlNode? use = sense.SelectSingleNode(@".//*[contains(concat(' ', normalize-space(@class), ' '), 'use')]");
                        string? useValue = use?.InnerText;
                        meaning.Usage = useValue?.Trim();

                        meaning.Examples = [];
                        HtmlNode? exampleList = sense.SelectSingleNode(@".//*[contains(concat(' ', normalize-space(@class), ' '), 'examples')]");
                        HtmlNodeCollection? exampleListItems = exampleList?.SelectNodes(@".//li");
                        if (exampleListItems?.Count > 0)
                        {
                            foreach (HtmlNode example in exampleListItems)
                            {
                                Example exmple = new();
                                meaning.Examples.Add(exmple);

                                HtmlNode? exampleText = example.SelectSingleNode(@"./*[contains(concat(' ', normalize-space(@class), ' '), 'x')]");
                                string? exampleTextValue = exampleText?.InnerText;
                                exmple.Text = exampleTextValue?.Trim();

                                HtmlNode? examplePreDescription = example.SelectSingleNode(@"./*[contains(concat(' ', normalize-space(@class), ' '), 'cf')]");
                                string? examplePreDescriptionValue = examplePreDescription?.InnerText;
                                exmple.Description = examplePreDescriptionValue?.Trim();
                            }
                        }

                        HtmlNode? topics = sense.SelectSingleNode(@".//*[contains(concat(' ', normalize-space(@class), ' '), 'topic-g')]");
                        HtmlNodeCollection? topicNames = topics?.SelectNodes(@".//*[contains(concat(' ', normalize-space(@class), ' '), 'topic_name')]");
                        List<string> topicNameValues = topicNames?.Select(x => x.InnerText.Trim()).Where(x => x != string.Empty)?.ToList() ?? [];
                        meaning.Topics = topicNameValues;
                    }
                }
            }
        }

        return word;
    }
}