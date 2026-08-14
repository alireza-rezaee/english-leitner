using System.Text.Json;
using EnglishLeitner.OxfordWebScraper;
using EnglishLeitner.OxfordWebScraper.Data;
using EnglishLeitner.OxfordWebScraper.DTOs;

string dataDir = "bin/data";
Directory.CreateDirectory(dataDir);
string resultsPath = $"{dataDir}/results.json";
string jsonResult = $"{dataDir}/db.json";

// Read Previous Results
List<Result> pageResults;
if (File.Exists(resultsPath))
{
    using FileStream fs = File.OpenRead(resultsPath);
    pageResults = await JsonSerializer.DeserializeAsync<List<Result>>(fs) ?? [];
}
else
    pageResults = [];

Scraper scraper = new();
string[] urlPaths = await Scraper.GetWordPathsAsync();

foreach (string urlPath in urlPaths)
{
    Result? result = pageResults.FirstOrDefault(x => x.RelativeUrl == urlPath);
    if (!string.IsNullOrWhiteSpace(result?.DownloadPath))
    {
        ColoredConsoleWriteLine($"{result} (exists)", ConsoleColor.Green);
        continue;
    }

    try
    {
        string html = await Scraper.ScrapHtmlAsync(urlPath);

        string fileName = Guid.NewGuid().ToString() + ".html";
        string filePath = Path.Combine(dataDir, fileName);

        await File.AppendAllTextAsync(filePath, html);

        result = new(urlPath, filePath);
        pageResults.Add(result);

        ColoredConsoleWriteLine(result.ToString(), ConsoleColor.Blue);
    }
    catch (Exception ex)
    {
        pageResults.Add(new(urlPath, string.Empty));
        ColoredConsoleWriteLine(ex.ToString(), isError: true);
    }
    finally
    {
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Save output
        using FileStream fs = File.OpenWrite(resultsPath);
        await JsonSerializer.SerializeAsync(fs, pageResults);
    }
}

ColoredConsoleWriteLine("Parsing Structures Strated", ConsoleColor.Blue);
List<Word> words = [];
foreach (Result pageResult in pageResults)
{
    string path = pageResult.DownloadPath;
    using FileStream fileStream = File.OpenRead(path);
    Word word = Scraper.Parse(fileStream, pageResult.RelativeUrl);
    words.Add(word);
}

if (words?.Count > 0)
{
    // export db as JSON
    ColoredConsoleWriteLine($"Saving db as JSON in {jsonResult}", ConsoleColor.Blue);
    using FileStream fs = File.OpenWrite(jsonResult);
    await JsonSerializer.SerializeAsync(fs, words);

    // export as SQLite database
    if (File.Exists(ApplicationDbContent.DbPath))
        File.Delete(ApplicationDbContent.DbPath);
    ColoredConsoleWriteLine($"Saving as sqlite db in {ApplicationDbContent.DbPath}", ConsoleColor.Blue);
    using ApplicationDbContent db = new();
    db.Database.EnsureCreated();
    await db.Words.AddRangeAsync(words);
    await db.SaveChangesAsync();
}

static void ColoredConsoleWriteLine(string value, ConsoleColor? color = null, bool isError = false)
{
    ConsoleColor defaultColor = Console.ForegroundColor;

    if (isError && color is null)
        color = ConsoleColor.Red;

    if (color is not null)
        Console.ForegroundColor = (ConsoleColor)color;

    if (isError)
        Console.Error.WriteLine(value);
    else
        Console.WriteLine(value);

    if (color is not null)
        Console.ForegroundColor = defaultColor;
}


record Result(string RelativeUrl, string DownloadPath);
