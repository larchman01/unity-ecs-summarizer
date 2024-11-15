using EnvDTE;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace unity_ecs_summarizer
{
    [Command(PackageIds.SummarizeComponentTypeSets)]
    internal sealed class SummarizeComponentTypeSets : BaseCommand<SummarizeComponentTypeSets>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var documentView = await GetActiveDocumentViewAsync();
            if (documentView == null) return;

            string documentText = documentView.TextBuffer.CurrentSnapshot.GetText();
            documentText = RemoveExistingXmlSummaries(documentText);

            var componentTypeSets = FindComponentTypeSets(documentText);
            if (componentTypeSets.Count == 0)
            {
                await VS.MessageBox.ShowAsync("unity_ecs_summarizer", "No ComponentTypeSet variables found.");
                return;
            }

            var typeSets = ExtractTypeSetDetails(documentText, componentTypeSets);
            string newDocumentText = GenerateXmlSummaries(documentText, typeSets);

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
            var xmlSummaryRegex = new Regex(@"(^\s*)/// <summary>\s*/// Components:.*?/// </summary>\s*", RegexOptions.Singleline | RegexOptions.Multiline);
            return xmlSummaryRegex.Replace(documentText, match => match.Groups[1].Value);
        }

        private static MatchCollection FindComponentTypeSets(string documentText)
        {
            var componentTypeSetRegex = new Regex(@"\b(private|protected|public|internal)?\s*ComponentTypeSet\b\s+_(\w+)\s*;");
            return componentTypeSetRegex.Matches(documentText);
        }

        private static Dictionary<string, (string Declaration, int StartIndex, List<string> Components, string Indentation)> ExtractTypeSetDetails(string documentText, MatchCollection matches)
        {
            Dictionary<string, (string Declaration, int StartIndex, List<string> Components, string Indentation)> typeSets = new();

            foreach (Match match in matches)
            {
                string typeName = match.Groups[2].Value;
                string fullDeclaration = match.Value;
                int declarationIndex = match.Index;

                int lineStartIndex = documentText.LastIndexOf('\n', declarationIndex) + 1;
                string line = documentText.Substring(lineStartIndex, declarationIndex - lineStartIndex);
                string indentation = Regex.Match(line, @"^\s*").Value;

                var components = ExtractComponents(documentText, typeName);
                if (components.Any())
                {
                    typeSets[typeName] = (fullDeclaration, declarationIndex, components, indentation);
                }
            }

            return typeSets;
        }

        private static List<string> ExtractComponents(string documentText, string typeName)
        {
            var assignRegex = new Regex($@"{typeName}\s*=\s*new\s*ComponentTypeSet\s*\((.*?)\);", RegexOptions.Singleline);
            var assignMatch = assignRegex.Match(documentText);

            if (assignMatch.Success)
            {
                string componentsBody = assignMatch.Groups[1].Value;
                return ExtractComponentList(componentsBody);
            }
            else
            {
                var fixedListRegex = new Regex($@"{typeName}\s*=\s*new\s*ComponentTypeSet\(new\s*FixedList128Bytes<ComponentType>\s*{{(.*?)}}\);", RegexOptions.Singleline);
                var fixedListMatch = fixedListRegex.Match(documentText);

                if (fixedListMatch.Success)
                {
                    string componentsBody = fixedListMatch.Groups[1].Value;
                    return ExtractComponentList(componentsBody);
                }
            }

            return new List<string>();
        }

        private static List<string> ExtractComponentList(string componentsBody)
        {
            var componentRegex = new Regex(@"ComponentType\.\w+<([^>]+)>");
            return componentRegex.Matches(componentsBody)
                .Cast<Match>()
                .Select(m => m.Groups[1].Value.Trim())
                .ToList();
        }

        private static string GenerateXmlSummaries(string documentText, Dictionary<string, (string Declaration, int StartIndex, List<string> Components, string Indentation)> typeSets)
        {
            var newDocumentText = documentText;
            foreach (var typeSet in typeSets.OrderByDescending(q => q.Value.StartIndex))
            {
                var (declaration, startIndex, components, indentation) = typeSet.Value;

                var summaryBuilder = new System.Text.StringBuilder();
                summaryBuilder.AppendLine($"{indentation}/// <summary>");
                summaryBuilder.AppendLine($"{indentation}/// Components: {string.Join(", ", components.Select(c => $"<see cref=\"{c}\" />"))}");
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
    }
}