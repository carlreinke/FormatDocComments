// Copyright Carl Reinke
//
// This file is part of a library that is licensed under the terms of the GNU
// Lesser General Public License Version 3 as published by the Free Software
// Foundation.
//
// This license does not grant rights under trademark law for use of any trade
// names, trademarks, or service marks.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Threading;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace FormatDocComments
{
    [Export]
    internal sealed class FormatDocCommentsService
    {
        private readonly JoinableTaskContext _joinableTaskContext;

        [ImportingConstructor]
        public FormatDocCommentsService(JoinableTaskContext joinableTaskContext)
        {
            _joinableTaskContext = joinableTaskContext;
        }

        public async Task<Document> FormatDocCommentsInDocumentAsync(Document document, CancellationToken cancellationToken)
        {
            var options = (OptionSet)await document.GetOptionsAsync(cancellationToken).ConfigureAwait(false);

            if (!options.GetOption(DocCommentFormattingOptions.WrapColumn).HasValue)
            {
                int? wrapColumn = await GetWrapColumnAsync(document, cancellationToken).ConfigureAwait(false);
                options = options.WithChangedOption(DocCommentFormattingOptions.WrapColumn, wrapColumn);
            }

            var changes = await DocCommentFormatter.FormatAsync(document, options, cancellationToken).ConfigureAwait(false);

            var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);

            return document.WithText(text.WithChanges(changes));
        }

        public async Task<Document> FormatDocCommentsInSelectionAsync(Document document, TextSpan selectionSpan, CancellationToken cancellationToken)
        {
            var options = (OptionSet)await document.GetOptionsAsync(cancellationToken).ConfigureAwait(false);

            if (!options.GetOption(DocCommentFormattingOptions.WrapColumn).HasValue)
            {
                int? wrapColumn = await GetWrapColumnAsync(document, cancellationToken).ConfigureAwait(false);
                options = options.WithChangedOption(DocCommentFormattingOptions.WrapColumn, wrapColumn);
            }

            var changes = await DocCommentFormatter.FormatAsync(document, selectionSpan, options, cancellationToken).ConfigureAwait(false);

            var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);

            return document.WithText(text.WithChanges(changes));
        }

        private async Task<int?> GetWrapColumnAsync(Document document, CancellationToken cancellationToken)
        {
            var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
            if (syntaxTree == null)
                return null;

            var options = document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(syntaxTree);

            if (options.TryGetValue("max_line_length", out string? maxLineLengthString))
                if (int.TryParse(maxLineLengthString, NumberStyles.None, CultureInfo.InvariantCulture, out int maxLineLength))
                    if (maxLineLength > 0)
                        return maxLineLength;

            if (options.TryGetValue("rulers", out string? rulersString))
            {
                int maxRuler = 0;

                foreach (string rulerString in rulersString.Split(','))
                    if (int.TryParse(rulerString, NumberStyles.AllowLeadingWhite, CultureInfo.InvariantCulture, out int ruler))
                        if (maxRuler < ruler)
                            maxRuler = ruler;

                if (maxRuler > 0)
                    return maxRuler;
            }

            await _joinableTaskContext.Factory.SwitchToMainThreadAsync(cancellationToken);

            return TextEditorGuidesSettings.GetGuideColumns().Max();
        }
    }
}
