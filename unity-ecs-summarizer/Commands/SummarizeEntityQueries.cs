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
            documentText = RemoveExistingXmlSummaries(documentText);

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

        private static string RemoveExistingXmlSummaries(string documentText)
        {
            var xmlSummaryRegex = new Regex(@"(^\s*)/// <summary>\s*(/// (WithAll|WithNone|WithAny|WithAbsent|WithDisabled|WithPresent):.*?(\s*///.*?)*?)\s*/// </summary>\s*", RegexOptions.Singleline | RegexOptions.Multiline);
            return xmlSummaryRegex.Replace(documentText, match => match.Groups[1].Value);
        }

        private static MatchCollection FindEntityQueries(string documentText)
        {
            var entityQueryRegex = new Regex(@"\b(private|protected|public|internal)?\s*EntityQuery\b\s+_(\w+)\s*;");
            return entityQueryRegex.Matches(documentText);
        }

        private static Dictionary<string, (string Declaration, int StartIndex, List<string> WithAll, List<string> WithNone, List<string> WithAny, List<string> WithAbsent, List<string> WithDisabled, List<string> WithPresent, string Indentation)> ExtractQueryDetails(string documentText, MatchCollection matches)
        {
            Dictionary<string, (string Declaration, int StartIndex, List<string> WithAll, List<string> WithNone, List<string> WithAny, List<string> WithAbsent, List<string> WithDisabled, List<string> WithPresent, string Indentation)> queries = new();

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

                    List<string> withAll = ExtractComponents(new Regex(@"\.WithAll<([^>]*)>"), queryBody);
                    List<string> withNone = ExtractComponents(new Regex(@"\.WithNone<([^>]*)>"), queryBody);
                    List<string> withAny = ExtractComponents(new Regex(@"\.WithAny<([^>]*)>"), queryBody);
                    List<string> withAbsent = ExtractComponents(new Regex(@"\.WithAbsent<([^>]*)>"), queryBody);
                    List<string> withDisabled = ExtractComponents(new Regex(@"\.WithDisabled<([^>]*)>"), queryBody);
                    List<string> withPresent = ExtractComponents(new Regex(@"\.WithPresent<([^>]*)>"), queryBody);

                    queries[queryName] = (fullDeclaration, declarationIndex, withAll, withNone, withAny, withAbsent, withDisabled, withPresent, indentation);
                }
            }

            return queries;
        }

        private static string GenerateXmlSummaries(string documentText, Dictionary<string, (string Declaration, int StartIndex, List<string> WithAll, List<string> WithNone, List<string> WithAny, List<string> WithAbsent, List<string> WithDisabled, List<string> WithPresent, string Indentation)> queries)
        {
            var newDocumentText = documentText;
            foreach (var query in queries.OrderByDescending(q => q.Value.StartIndex))
            {
                var (declaration, startIndex, withAll, withNone, withAny, withAbsent, withDisabled, withPresent, indentation) = query.Value;

                var summaryBuilder = new System.Text.StringBuilder();
                summaryBuilder.AppendLine($"{indentation}/// <summary>");
                AppendComponentList(summaryBuilder, indentation, "WithAll", withAll);
                AppendComponentList(summaryBuilder, indentation, "WithNone", withNone);
                AppendComponentList(summaryBuilder, indentation, "WithAny", withAny);
                AppendComponentList(summaryBuilder, indentation, "WithAbsent", withAbsent);
                AppendComponentList(summaryBuilder, indentation, "WithDisabled", withDisabled);
                AppendComponentList(summaryBuilder, indentation, "WithPresent", withPresent);
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

        private static void AppendComponentList(System.Text.StringBuilder builder, string indentation, string clause, List<string> components)
        {
            if (components.Any())
            {
                builder.AppendLine($"{indentation}/// {clause}: {string.Join(", ", components.Select(c => $"<see cref=\"{c}\" />"))} <br />");
            }
        }
    }
}