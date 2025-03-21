using EnvDTE;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace unity_ecs_summarizer
{
    [Command(PackageIds.SummarizeEntityQueries)]
    internal sealed class SummarizeEntityQueries : BaseCommand<SummarizeEntityQueries>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var documentView = await GetActiveDocumentViewAsync();
            if (documentView == null) return;

            string documentText = documentView.TextBuffer.CurrentSnapshot.GetText();
            documentText = RemoveExistingEntityQuerySummaries(documentText);

            var entityQueries = FindEntityQueries(documentText);
            if (entityQueries.Count == 0)
            {
                await VS.MessageBox.ShowAsync("unity_ecs_summarizer", "No EntityQuery variables found.");
                return;
            }

            var queries = ExtractQueryDetails(documentText, entityQueries);
            string newDocumentText = GenerateXmlSummaries(documentText, queries);

            ApplyModifiedText(documentView, newDocumentText);
        }

        private static async Task<DocumentView> GetActiveDocumentViewAsync()
        {
            var documentView = await VS.Documents.GetActiveDocumentViewAsync();
            if (documentView == null || documentView.TextBuffer == null)
            {
                await VS.MessageBox.ShowErrorAsync("unity_ecs_summarizer", "No active document or text buffer found.");
                return null;
            }
            return documentView;
        }

        private static string RemoveExistingEntityQuerySummaries(string documentText)
        {
            // Regex to match any XML summary for EntityQuery variables
            var xmlSummaryRegex = new Regex(@"(^\s*)/// <summary>(.*?)/// </summary>\s*", RegexOptions.Singleline | RegexOptions.Multiline);

            // List of keywords to check for in the summary content
            var keywords = new[]
            {
                "WithAll",
                "WithAllChunkComponent",
                "WithAllChunkComponentRW",
                "WithAllRW",
                "WithAny",
                "WithAnyChunkComponent",
                "WithAnyChunkComponentRW",
                "WithAnyRW",
                "WithAspect",
                "WithDisabled",
                "WithDisabledRW",
                "WithNone",
                "WithNoneChunkComponent",
                "WithOptions",
                "WithPresent",
                "WithPresentChunkComponent",
                "WithPresentChunkComponentRW",
                "WithPresentRW"
            };

            return xmlSummaryRegex.Replace(documentText, match =>
            {
                // Check if the summary content contains any of the specified keywords
                if (keywords.Any(keyword => match.Groups[2].Value.Contains(keyword)))
                {
                    // If it does, remove the summary by returning only the leading whitespace
                    return match.Groups[1].Value;
                }

                // Otherwise, return the original match (do not remove the summary)
                return match.Value;
            });
        }

        private static MatchCollection FindEntityQueries(string documentText)
        {
            var entityQueryRegex = new Regex(@"\b(private|protected|public|internal)?\s*EntityQuery\b\s+_(\w+)\s*;");
            return entityQueryRegex.Matches(documentText);
        }

        private static Dictionary<string, (string Declaration, int StartIndex, List<List<string>> WithAll, List<List<string>> WithAllChunkComponent, List<List<string>> WithAllChunkComponentRW, List<List<string>> WithAllRW, List<List<string>> WithAny, List<List<string>> WithAnyChunkComponent, List<List<string>> WithAnyChunkComponentRW, List<List<string>> WithAnyRW, List<List<string>> WithAspect, List<List<string>> WithDisabled, List<List<string>> WithDisabledRW, List<List<string>> WithNone, List<List<string>> WithNoneChunkComponent, List<List<string>> WithOptions, List<List<string>> WithPresent, List<List<string>> WithPresentChunkComponent, List<List<string>> WithPresentChunkComponentRW, List<List<string>> WithPresentRW, string Indentation, int QuerySegmentCount)> ExtractQueryDetails(string documentText, MatchCollection matches)
        {
            var queries = new Dictionary<string, (string Declaration, int StartIndex, List<List<string>> WithAll, List<List<string>> WithAllChunkComponent, List<List<string>> WithAllChunkComponentRW, List<List<string>> WithAllRW, List<List<string>> WithAny, List<List<string>> WithAnyChunkComponent, List<List<string>> WithAnyChunkComponentRW, List<List<string>> WithAnyRW, List<List<string>> WithAspect, List<List<string>> WithDisabled, List<List<string>> WithDisabledRW, List<List<string>> WithNone, List<List<string>> WithNoneChunkComponent, List<List<string>> WithOptions, List<List<string>> WithPresent, List<List<string>> WithPresentChunkComponent, List<List<string>> WithPresentChunkComponentRW, List<List<string>> WithPresentRW, string Indentation, int QuerySegmentCount)>();

            foreach (Match match in matches)
            {
                string queryName = match.Groups[2].Value;
                string fullDeclaration = match.Value;
                int declarationIndex = match.Index;

                int lineStartIndex = documentText.LastIndexOf('\n', declarationIndex) + 1;
                string line = documentText.Substring(lineStartIndex, declarationIndex - lineStartIndex);
                string indentation = Regex.Match(line, @"^\s*").Value;

                var assignRegex = new Regex($@"{queryName}\s*=\s*SystemAPI\.QueryBuilder\(\)\s*(.*?)\.Build\(\);", RegexOptions.Singleline);
                var assignMatch = assignRegex.Match(documentText);

                if (assignMatch.Success)
                {
                    string queryBody = assignMatch.Groups[1].Value;

                    // Split the query body by .AddAdditionalQuery()
                    var querySegments = Regex.Split(queryBody, @"\.AddAdditionalQuery\(\)\s*");
                    int querySegmentCount = querySegments.Length;

                    var withAll = new List<List<string>>();
                    var withAllChunkComponent = new List<List<string>>();
                    var withAllChunkComponentRW = new List<List<string>>();
                    var withAllRW = new List<List<string>>();
                    var withAny = new List<List<string>>();
                    var withAnyChunkComponent = new List<List<string>>();
                    var withAnyChunkComponentRW = new List<List<string>>();
                    var withAnyRW = new List<List<string>>();
                    var withAspect = new List<List<string>>();
                    var withDisabled = new List<List<string>>();
                    var withDisabledRW = new List<List<string>>();
                    var withNone = new List<List<string>>();
                    var withNoneChunkComponent = new List<List<string>>();
                    var withOptions = new List<List<string>>();
                    var withPresent = new List<List<string>>();
                    var withPresentChunkComponent = new List<List<string>>();
                    var withPresentChunkComponentRW = new List<List<string>>();
                    var withPresentRW = new List<List<string>>();

                    foreach (var segment in querySegments)
                    {
                        withAll.Add(ExtractComponents(new Regex(@"\.WithAll<([^>]*)>"), segment));
                        withAllChunkComponent.Add(ExtractComponents(new Regex(@"\.WithAllChunkComponent<([^>]*)>"), segment));
                        withAllChunkComponentRW.Add(ExtractComponents(new Regex(@"\.WithAllChunkComponentRW<([^>]*)>"), segment));
                        withAllRW.Add(ExtractComponents(new Regex(@"\.WithAllRW<([^>]*)>"), segment));
                        withAny.Add(ExtractComponents(new Regex(@"\.WithAny<([^>]*)>"), segment));
                        withAnyChunkComponent.Add(ExtractComponents(new Regex(@"\.WithAnyChunkComponent<([^>]*)>"), segment));
                        withAnyChunkComponentRW.Add(ExtractComponents(new Regex(@"\.WithAnyChunkComponentRW<([^>]*)>"), segment));
                        withAnyRW.Add(ExtractComponents(new Regex(@"\.WithAnyRW<([^>]*)>"), segment));
                        withAspect.Add(ExtractComponents(new Regex(@"\.WithAspect<([^>]*)>"), segment));
                        withDisabled.Add(ExtractComponents(new Regex(@"\.WithDisabled<([^>]*)>"), segment));
                        withDisabledRW.Add(ExtractComponents(new Regex(@"\.WithDisabledRW<([^>]*)>"), segment));
                        withNone.Add(ExtractComponents(new Regex(@"\.WithNone<([^>]*)>"), segment));
                        withNoneChunkComponent.Add(ExtractComponents(new Regex(@"\.WithNoneChunkComponent<([^>]*)>"), segment));
                        withOptions.Add(ExtractEntityQueryOptions(segment));
                        withPresent.Add(ExtractComponents(new Regex(@"\.WithPresent<([^>]*)>"), segment));
                        withPresentChunkComponent.Add(ExtractComponents(new Regex(@"\.WithPresentChunkComponent<([^>]*)>"), segment));
                        withPresentChunkComponentRW.Add(ExtractComponents(new Regex(@"\.WithPresentChunkComponentRW<([^>]*)>"), segment));
                        withPresentRW.Add(ExtractComponents(new Regex(@"\.WithPresentRW<([^>]*)>"), segment));
                    }

                    queries[queryName] = (fullDeclaration, declarationIndex, withAll, withAllChunkComponent, withAllChunkComponentRW, withAllRW, withAny, withAnyChunkComponent, withAnyChunkComponentRW, withAnyRW, withAspect, withDisabled, withDisabledRW, withNone, withNoneChunkComponent, withOptions, withPresent, withPresentChunkComponent, withPresentChunkComponentRW, withPresentRW, indentation, querySegmentCount);
                }
            }

            return queries;
        }

        private static List<string> ExtractEntityQueryOptions(string queryBody)
        {
            var optionsRegex = new Regex(@"\.WithOptions\(\s*EntityQueryOptions\.([^)]+)\)");
            return optionsRegex.Matches(queryBody)
                .Cast<Match>()
                .Select(m => m.Groups[1].Value.Trim())
                .ToList();
        }

        private static string GenerateXmlSummaries(string documentText, Dictionary<string, (string Declaration, int StartIndex, List<List<string>> WithAll, List<List<string>> WithAllChunkComponent, List<List<string>> WithAllChunkComponentRW, List<List<string>> WithAllRW, List<List<string>> WithAny, List<List<string>> WithAnyChunkComponent, List<List<string>> WithAnyChunkComponentRW, List<List<string>> WithAnyRW, List<List<string>> WithAspect, List<List<string>> WithDisabled, List<List<string>> WithDisabledRW, List<List<string>> WithNone, List<List<string>> WithNoneChunkComponent, List<List<string>> WithOptions, List<List<string>> WithPresent, List<List<string>> WithPresentChunkComponent, List<List<string>> WithPresentChunkComponentRW, List<List<string>> WithPresentRW, string Indentation, int QuerySegmentCount)> queries)
        {
            var newDocumentText = documentText;
            foreach (var query in queries.OrderByDescending(q => q.Value.StartIndex))
            {
                var (declaration, startIndex, withAll, withAllChunkComponent, withAllChunkComponentRW, withAllRW, withAny, withAnyChunkComponent, withAnyChunkComponentRW, withAnyRW, withAspect, withDisabled, withDisabledRW, withNone, withNoneChunkComponent, withOptions, withPresent, withPresentChunkComponent, withPresentChunkComponentRW, withPresentRW, indentation, querySegmentCount) = query.Value;

                var summaryBuilder = new System.Text.StringBuilder();
                summaryBuilder.AppendLine($"{indentation}/// <summary>");

                // Check if there are multiple queries
                bool hasMultipleQueries = querySegmentCount > 1;

                for (int i = 0; i < querySegmentCount; i++)
                {
                    // Add "Query X" label only if there are multiple queries
                    if (hasMultipleQueries)
                    {
                        // For the first query, just add the label with indentation
                        if (i == 0)
                        {
                            summaryBuilder.AppendLine($"{indentation}/// Query {i + 1}: <br />");
                        }
                        // For subsequent queries, add <br /> before the label on the same line
                        else
                        {
                            summaryBuilder.AppendLine($"{indentation}/// <br /> Query {i + 1}: <br />");
                        }
                    }

                    var types = new List<(string Clause, List<string> Components)>
                    {
                        ("WithAll", withAll[i]),
                        ("WithAllChunkComponent", withAllChunkComponent[i]),
                        ("WithAllChunkComponentRW", withAllChunkComponentRW[i]),
                        ("WithAllRW", withAllRW[i]),
                        ("WithAny", withAny[i]),
                        ("WithAnyChunkComponent", withAnyChunkComponent[i]),
                        ("WithAnyChunkComponentRW", withAnyChunkComponentRW[i]),
                        ("WithAnyRW", withAnyRW[i]),
                        ("WithAspect", withAspect[i]),
                        ("WithDisabled", withDisabled[i]),
                        ("WithDisabledRW", withDisabledRW[i]),
                        ("WithNone", withNone[i]),
                        ("WithNoneChunkComponent", withNoneChunkComponent[i]),
                        ("WithOptions", withOptions[i]),
                        ("WithPresent", withPresent[i]),
                        ("WithPresentChunkComponent", withPresentChunkComponent[i]),
                        ("WithPresentChunkComponentRW", withPresentChunkComponentRW[i]),
                        ("WithPresentRW", withPresentRW[i])
                    };

                    // Remove types with no components
                    types = types.Where(t => t.Components.Any()).ToList();

                    // Append each type, passing whether it's the last type in the query
                    for (int j = 0; j < types.Count; j++)
                    {
                        bool isLastTypeInQuery = (j == types.Count - 1);
                        AppendComponentList(summaryBuilder, indentation, types[j].Clause, types[j].Components, isLastTypeInQuery);
                    }
                }

                summaryBuilder.AppendLine($"{indentation}/// </summary>");

                int lineStartIndex = newDocumentText.LastIndexOf('\n', startIndex) + 1;
                newDocumentText = newDocumentText.Insert(lineStartIndex, summaryBuilder.ToString());
            }

            return newDocumentText;
        }

        private static void ApplyModifiedText(DocumentView documentView, string newDocumentText)
        {
            var edit = documentView.TextBuffer.CreateEdit();
            edit.Replace(0, documentView.TextBuffer.CurrentSnapshot.Length, newDocumentText);
            edit.Apply();
        }

        private static List<string> ExtractComponents(Regex regex, string queryBody)
        {
            return regex.Matches(queryBody)
                .Cast<Match>()
                .SelectMany(m => m.Groups[1].Value.Split(',').Select(c => c.Trim()))
                .ToList();
        }

        private static void AppendComponentList(System.Text.StringBuilder builder, string indentation, string clause, List<string> components, bool isLastTypeInQuery)
        {
            if (components.Any())
            {
                string componentList;
                if (clause == "WithOptions")
                {
                    // Format WithOptions as "WithOptions: <see cref="EntityQueryOptions.Default" />"
                    componentList = $"WithOptions: {string.Join(", ", components.Select(c => $"<see cref=\"EntityQueryOptions.{c}\" />"))}";
                }
                else
                {
                    // Format other types as "<Clause>: <see cref="Component" />"
                    componentList = $"{clause}: {string.Join(", ", components.Select(c => $"<see cref=\"{c}\" />"))}";
                }

                builder.Append($"{indentation}/// {componentList}");

                // Add <br /> unless it's the last type in the query
                if (!isLastTypeInQuery)
                {
                    builder.Append(" <br />");
                }

                builder.AppendLine(); // Move to the next line
            }
        }
    }
}