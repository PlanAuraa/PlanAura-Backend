using System.Globalization;
using System.Text.RegularExpressions;
using Planura.Core.Application.Abstraction.Contract;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Planura.Infrastructure.Contract
{
    // Renders a ContractPdfModel into a premium, branded PDF using QuestPDF. This service owns
    // all layout/visual decisions and has no knowledge of Gemini or how the text was produced.
    public class PdfService : IPdfService
    {
        static PdfService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerateContractPdf(ContractPdfModel model)
        {
            var document = new ContractDocument(model);
            return document.GeneratePdf();
        }

        // ---- Planura brand palette (matches PlanAura-Frontend/src/styles.css design tokens) ----
        private static class Palette
        {
            public const string Primary = "#E8654A";
            public const string PrimaryDark = "#D6543A";
            public const string Peach = "#F6A26F";
            public const string RoseGold = "#C48B72";
            public const string Rose = "#E0828C";
            public const string Charcoal = "#241F1C";
            public const string DarkGray = "#4A433D";
            public const string Secondary = "#8A8074";
            public const string OnSecondaryContainer = "#6B6357";
            public const string Surface = "#FDFAF7";
            public const string SoftBeige = "#F6EFE6";
            public const string SurfaceContainer = "#F5EDE3";
            public const string SubtleBorder = "#ECE1D3";
            public const string White = "#FFFFFF";
            public const string FooterMuted = "#C9C2BA";
            public const string Watermark = "#12241F1C"; // charcoal at ~7% alpha
        }

        private sealed class ContractSection
        {
            public int? Number { get; init; }
            public string Title { get; init; } = string.Empty;
            public List<string> Paragraphs { get; } = new();

            /// <summary>Enumerated points rendered as (a), (b), (c)… Only ever populated on the structured path.</summary>
            public List<string> Items { get; } = new();
        }

        /// <summary>
        /// Builds the render model from pre-structured clauses. Clause count and shape are whatever the
        /// document actually needs - nothing here assumes a fixed set of sections.
        /// </summary>
        private static List<ContractSection> BuildSections(IReadOnlyList<ContractSectionContent> sections)
        {
            var result = new List<ContractSection>(sections.Count);
            var number = 1;

            foreach (var source in sections)
            {
                var section = new ContractSection
                {
                    Number = number++,
                    Title = (source.Title ?? string.Empty).Trim().TrimEnd('.')
                };

                section.Paragraphs.AddRange(
                    source.Paragraphs.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
                section.Items.AddRange(
                    source.Items.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim()));

                result.Add(section);
            }

            return result;
        }

        private static readonly Regex SectionHeadingRegex =
            new(@"^SECTION\s+(\d+)\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static List<ContractSection> ParseSections(string contractBody)
        {
            var lines = contractBody.Replace("\r\n", "\n").Split('\n');
            var sections = new List<ContractSection>();
            ContractSection? current = null;
            var paragraphBuffer = new List<string>();

            void FlushParagraph()
            {
                if (paragraphBuffer.Count == 0)
                {
                    return;
                }

                var paragraph = string.Join(" ", paragraphBuffer).Trim();
                paragraphBuffer.Clear();

                if (paragraph.Length == 0)
                {
                    return;
                }

                current ??= AddSection(sections, null, string.Empty);
                current.Paragraphs.Add(paragraph);
            }

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                if (line.Length == 0)
                {
                    FlushParagraph();
                    continue;
                }

                var match = SectionHeadingRegex.Match(line);
                if (match.Success)
                {
                    FlushParagraph();
                    current = AddSection(sections, int.Parse(match.Groups[1].Value), match.Groups[2].Value.Trim().TrimEnd('.'));
                    continue;
                }

                paragraphBuffer.Add(line);
            }

            FlushParagraph();

            // Fallback: if the model ignored the "SECTION n:" formatting instruction entirely,
            // fall back to rendering the raw text as clean, blank-line-separated paragraphs
            // inside a single card rather than showing nothing.
            if (sections.Count == 0 || sections.TrueForAll(s => s.Number is null))
            {
                var fallback = AddSection(new List<ContractSection>(), null, "CONTRACT TERMS");
                var paragraphs = contractBody
                    .Replace("\r\n", "\n")
                    .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Replace("\n", " ").Trim())
                    .Where(p => p.Length > 0);

                fallback.Paragraphs.AddRange(paragraphs);
                return new List<ContractSection> { fallback };
            }

            return sections;
        }

        private static ContractSection AddSection(List<ContractSection> sections, int? number, string title)
        {
            var section = new ContractSection { Number = number, Title = title };
            sections.Add(section);
            return section;
        }

        private sealed class ContractDocument : IDocument
        {
            private readonly ContractPdfModel _model;
            private readonly List<ContractSection> _sections;

            public ContractDocument(ContractPdfModel model)
            {
                _model = model;

                // Structured clauses render directly; the SECTION-heading parser stays as the path for
                // documents still generated as free prose (the Vendor Partnership Agreement).
                _sections = model.Sections.Count > 0
                    ? BuildSections(model.Sections)
                    : ParseSections(model.ContractBody ?? string.Empty);
            }

            public DocumentMetadata GetMetadata()
            {
                var metadata = DocumentMetadata.Default;
                metadata.Title = $"Planura {_model.DocumentTitle} - {_model.ContractId}";
                metadata.Author = "Planura";
                metadata.Subject = _model.DocumentTitle;
                metadata.Creator = "Planura Contract Generator";
                return metadata;
            }

            public DocumentSettings GetSettings() => DocumentSettings.Default;

            public void Compose(IDocumentContainer container)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);
                    page.Content().Element(ComposeCoverPage);
                });

                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginHorizontal(40);
                    page.MarginVertical(32);
                    // No explicit FontFamily: QuestPDF's bundled "Lato" font is used automatically,
                    // guaranteeing consistent rendering in any deployment environment.
                    page.DefaultTextStyle(x => x.FontSize(10).FontColor(Palette.Charcoal));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(ComposeContent);
                    page.Footer().Element(ComposeFooter);
                });
            }

            // ---------------------------------------------------------------- Cover page

            private void ComposeCoverPage(IContainer container)
            {
                container.Background(Palette.White).Column(column =>
                {
                    column.Item().Height(230).Background(Palette.Primary).Padding(44).Column(hero =>
                    {
                        hero.Item().Text("PLANURA").FontSize(28).Bold().FontColor(Palette.White);
                        hero.Item().PaddingTop(2).Text("Event & Vendor Booking Platform").FontSize(10).FontColor(Palette.White);

                        hero.Item().PaddingTop(46).Text(_model.DocumentTitle.ToUpperInvariant()).FontSize(24).Bold().FontColor(Palette.White);
                        hero.Item().PaddingTop(4)
                            .Text(_model.DocumentTagline)
                            .FontSize(10.5f).FontColor(Palette.White);
                    });

                    column.Item().Height(5).Background(Palette.RoseGold);

                    column.Item().Padding(44).Column(body =>
                    {
                        body.Spacing(16);

                        body.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("CONTRACT ID").FontSize(8.5f).Bold().FontColor(Palette.Secondary);
                                c.Item().Text(_model.ContractId).FontSize(13).Bold().FontColor(Palette.Charcoal);
                            });
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().AlignRight().Text("GENERATED ON").FontSize(8.5f).Bold().FontColor(Palette.Secondary);
                                c.Item().AlignRight().Text(_model.GeneratedDate.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture)).FontSize(13).Bold().FontColor(Palette.Charcoal);
                            });
                        });

                        body.Item().LineHorizontal(1).LineColor(Palette.SubtleBorder);

                        body.Item().Text("PARTIES TO THIS AGREEMENT").FontSize(10.5f).Bold().FontColor(Palette.Primary);

                        body.Item().Row(row =>
                        {
                            row.RelativeItem().Element(c => PartyCard(c, _model.PartyA));
                            row.ConstantItem(18);
                            row.RelativeItem().Element(c => PartyCard(c, _model.PartyB));
                        });

                        if (_model.SummaryItems.Count > 0)
                        {
                            body.Item().Element(ComposeSummaryCard);
                        }

                        body.Item().PaddingTop(4).Text(
                                "This document was generated electronically by the Planura platform. Please review the full " +
                                "terms on the following pages before signing.")
                            .FontSize(8.5f).FontColor(Palette.Secondary).LineHeight(1.3f);
                    });

                    column.Item().Height(42).Background(Palette.Charcoal).Padding(10).Row(row =>
                    {
                        row.RelativeItem().AlignMiddle().Text("planura.app").FontSize(9).FontColor(Palette.White);
                        row.RelativeItem().AlignMiddle().AlignRight()
                            .Text("Confidential - prepared exclusively for the named parties")
                            .FontSize(8).FontColor(Palette.FooterMuted);
                    });
                });
            }

            private static void PartyCard(IContainer container, ContractPartyDto party)
            {
                container
                    .Background(Palette.SoftBeige)
                    .Border(1).BorderColor(Palette.SubtleBorder)
                    .CornerRadius(10)
                    .Padding(14)
                    .Column(c =>
                    {
                        c.Spacing(3);
                        c.Item().Text(party.Label).FontSize(8).Bold().FontColor(Palette.RoseGold);
                        c.Item().Text(string.IsNullOrWhiteSpace(party.Name) ? "N/A" : party.Name).FontSize(12.5f).Bold().FontColor(Palette.Charcoal);
                        c.Item().Text($"Representative: {(string.IsNullOrWhiteSpace(party.RepresentativeName) ? "N/A" : party.RepresentativeName)}")
                            .FontSize(8.5f).FontColor(Palette.Secondary);
                    });
            }

            private void ComposeSummaryCard(IContainer container)
            {
                // Pad to an even count so the 4-column (2 label/value pairs per row) grid always
                // fills cleanly, regardless of how many facts a given contract type supplies.
                var items = _model.SummaryItems.ToList();
                if (items.Count % 2 != 0)
                {
                    items.Add(new ContractSummaryItem(string.Empty, string.Empty));
                }

                container
                    .Background(Palette.SurfaceContainer)
                    .Border(1).BorderColor(Palette.SubtleBorder)
                    .CornerRadius(10)
                    .Padding(16)
                    .Column(c =>
                    {
                        c.Spacing(8);
                        c.Item().Text("SUMMARY").FontSize(9).Bold().FontColor(Palette.Primary);

                        c.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(1f);
                                cols.RelativeColumn(1.3f);
                                cols.RelativeColumn(1f);
                                cols.RelativeColumn(1.3f);
                            });

                            foreach (var item in items)
                            {
                                AddSummaryCell(table, item.Label, item.Value);
                            }
                        });
                    });
            }

            private static void AddSummaryCell(TableDescriptor table, string label, string value)
            {
                table.Cell().PaddingVertical(3).Text(label).FontSize(8).FontColor(Palette.Secondary);
                table.Cell().PaddingVertical(3).Text(value).FontSize(9.5f).Bold().FontColor(Palette.Charcoal);
            }

            // ---------------------------------------------------------------- Content pages

            private void ComposeHeader(IContainer container)
            {
                container.Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("PLANURA").FontSize(13).Bold().FontColor(Palette.Primary);
                            c.Item().Text(_model.DocumentTitle).FontSize(8).FontColor(Palette.Secondary);
                        });

                        row.ConstantItem(180).Column(c =>
                        {
                            c.Item().AlignRight().Text($"Contract ID: {_model.ContractId}").FontSize(8).FontColor(Palette.Secondary);
                            c.Item().AlignRight().Text($"Generated: {_model.GeneratedDate.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)}").FontSize(8).FontColor(Palette.Secondary);
                        });
                    });

                    column.Item().PaddingTop(8).LineHorizontal(1).LineColor(Palette.SubtleBorder);
                });
            }

            private void ComposeFooter(IContainer container)
            {
                container.Column(column =>
                {
                    column.Item().LineHorizontal(1).LineColor(Palette.SubtleBorder);
                    column.Item().PaddingTop(6).Row(row =>
                    {
                        row.RelativeItem().Text($"Confidential - Planura {_model.DocumentTitle}").FontSize(7.5f).FontColor(Palette.Secondary);

                        row.RelativeItem().AlignCenter().Text(_model.ContractId).FontSize(7.5f).FontColor(Palette.Secondary);

                        row.RelativeItem().AlignRight().Text(text =>
                        {
                            text.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Palette.Secondary));
                            text.Span("Page ");
                            text.CurrentPageNumber();
                            text.Span(" of ");
                            text.TotalPages();
                        });
                    });
                });
            }

            private void ComposeContent(IContainer container)
            {
                container.Layers(layers =>
                {
                    layers.Layer().Element(ComposeWatermark);

                    layers.PrimaryLayer().PaddingTop(12).Column(column =>
                    {
                        column.Spacing(12);

                        column.Item().Text(_model.DocumentTitle.ToUpperInvariant()).FontSize(15).Bold().FontColor(Palette.Charcoal);
                        column.Item().Text(_model.IntroParagraph).FontSize(9).FontColor(Palette.Secondary);

                        foreach (var section in _sections)
                        {
                            column.Item().Element(c => ComposeSectionCard(c, section));
                        }

                        column.Item().Element(ComposeSignatureBlock);
                    });
                });
            }

            private static void ComposeWatermark(IContainer container)
            {
                container
                    .AlignMiddle()
                    .AlignCenter()
                    .Rotate(-30)
                    .Text("PLANURA")
                    .FontSize(64)
                    .Bold()
                    .FontColor(Palette.Watermark);
            }

            private static void ComposeSectionCard(IContainer container, ContractSection section)
            {
                container
                    .Background(Palette.White)
                    .Border(1).BorderColor(Palette.SubtleBorder)
                    .CornerRadius(8)
                    .Row(outer =>
                    {
                        outer.ConstantItem(4).Background(section.Number.HasValue ? Palette.Primary : Palette.RoseGold);

                        outer.RelativeItem().Padding(15).Column(c =>
                        {
                            c.Spacing(6);

                            if (section.Number.HasValue)
                            {
                                c.Item().Row(row =>
                                {
                                    row.ConstantItem(24).Height(24).Background(Palette.Primary).CornerRadius(12)
                                        .AlignMiddle().AlignCenter()
                                        .Text(section.Number.Value.ToString()).FontSize(10).Bold().FontColor(Palette.White);

                                    row.RelativeItem().PaddingLeft(10).AlignMiddle()
                                        .Text(section.Title.ToUpperInvariant()).FontSize(10.5f).Bold().FontColor(Palette.Charcoal);
                                });
                            }
                            else if (!string.IsNullOrWhiteSpace(section.Title))
                            {
                                c.Item().Text(section.Title.ToUpperInvariant()).FontSize(10.5f).Bold().FontColor(Palette.Charcoal);
                            }

                            foreach (var paragraph in section.Paragraphs)
                            {
                                c.Item().Text(paragraph).FontSize(9.3f).FontColor(Palette.Charcoal).LineHeight(1.35f).Justify();
                            }

                            for (var i = 0; i < section.Items.Count; i++)
                            {
                                var marker = ItemMarker(i);
                                var item = section.Items[i];

                                c.Item().PaddingLeft(6).Row(row =>
                                {
                                    row.ConstantItem(22).Text(marker).FontSize(9.3f).Bold().FontColor(Palette.RoseGold);
                                    row.RelativeItem()
                                        .Text(item).FontSize(9.3f).FontColor(Palette.Charcoal).LineHeight(1.35f).Justify();
                                });
                            }
                        });
                    });
            }

            /// <summary>(a), (b) … (z), then (aa), (ab) … so a long clause list never runs out of markers.</summary>
            private static string ItemMarker(int index)
            {
                var label = string.Empty;
                var n = index;

                do
                {
                    label = (char)('a' + (n % 26)) + label;
                    n = (n / 26) - 1;
                }
                while (n >= 0);

                return $"({label})";
            }

            private void ComposeSignatureBlock(IContainer container)
            {
                container
                    .Background(Palette.SoftBeige)
                    .Border(1).BorderColor(Palette.SubtleBorder)
                    .CornerRadius(10)
                    .Padding(18)
                    .Column(c =>
                    {
                        c.Spacing(12);

                        c.Item().Text("SIGNATURES & ACKNOWLEDGMENT").FontSize(10.5f).Bold().FontColor(Palette.Primary);
                        c.Item().Text(
                                $"By signing below, the {ToTitleCase(_model.PartyA.Label)} and the {ToTitleCase(_model.PartyB.Label)} each " +
                                $"acknowledge that they have read, understood, and agree to be bound by the terms of this {_model.DocumentTitle}.")
                            .FontSize(8.7f).FontColor(Palette.Secondary);

                        c.Item().Row(row =>
                        {
                            row.RelativeItem().Element(c2 => ComposeSignatureBox(c2, _model.PartyA));
                            row.ConstantItem(20);
                            row.RelativeItem().Element(c2 => ComposeSignatureBox(c2, _model.PartyB));
                        });

                        c.Item().PaddingTop(4).Row(row =>
                        {
                            row.RelativeItem().Text("Facilitated by Planura - planura.app").FontSize(8).FontColor(Palette.Secondary);
                            row.RelativeItem().AlignRight().Text($"Contract ID: {_model.ContractId}").FontSize(8).FontColor(Palette.Secondary);
                        });
                    });
            }

            private static void ComposeSignatureBox(IContainer container, ContractPartyDto party)
            {
                container
                    .Background(Palette.White)
                    .Border(1).BorderColor(Palette.SubtleBorder)
                    .CornerRadius(8)
                    .Padding(16)
                    .Column(c =>
                    {
                        c.Item().Text(party.Label).FontSize(8).Bold().FontColor(Palette.RoseGold);
                        c.Item().PaddingTop(2).Text(string.IsNullOrWhiteSpace(party.Name) ? "N/A" : party.Name).FontSize(11).Bold().FontColor(Palette.Charcoal);

                        c.Item().PaddingTop(20).LineHorizontal(1).LineColor(Palette.Charcoal);
                        c.Item().PaddingTop(2).Text("Signature").FontSize(7.5f).FontColor(Palette.Secondary);

                        c.Item().PaddingTop(12).LineHorizontal(1).LineColor(Palette.SubtleBorder);
                        c.Item().PaddingTop(2)
                            .Text($"Printed Name: {(string.IsNullOrWhiteSpace(party.RepresentativeName) ? "N/A" : party.RepresentativeName)}")
                            .FontSize(8.5f).FontColor(Palette.Charcoal);

                        c.Item().PaddingTop(12).LineHorizontal(1).LineColor(Palette.SubtleBorder);
                        c.Item().PaddingTop(2).Text("Date: ____________________").FontSize(8.5f).FontColor(Palette.Charcoal);
                    });
            }

            private static string ToTitleCase(string label)
            {
                if (string.IsNullOrWhiteSpace(label))
                {
                    return "Party";
                }

                var lower = label.Trim().ToLowerInvariant();
                return char.ToUpperInvariant(lower[0]) + lower[1..];
            }
        }
    }
}
