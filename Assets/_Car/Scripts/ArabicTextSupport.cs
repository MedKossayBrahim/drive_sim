using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// Minimal Arabic text support for Unity's IMGUI / legacy UI Text.
///
/// Unity's built-in text rendering draws strings left-to-right and does not
/// perform Arabic contextual shaping or bidi reordering. Without help, Arabic
/// is therefore drawn as disconnected, isolated letters in the wrong order.
///
/// This helper does the two things Unity leaves out:
///   1. Contextual shaping: each Arabic letter is replaced with its isolated,
///      final, initial or medial presentation form, and lam-alef ligatures are
///      created.
///   2. Bidi reordering: the shaped string is rearranged into visual order while
///      keeping Latin words and numbers in their correct left-to-right order.
///
/// Call <see cref="PrepareForDisplay"/> on any string that may contain Arabic
/// before handing it to GUI.Label, Text.text, etc.
/// </summary>
public static class ArabicTextSupport
{
    private const char NoGlyph = '\0';

    private struct LetterForms
    {
        public char Isolated;
        public char Final;
        public char Initial;
        public char Medial;

        // A letter can connect to the previous letter when it has a final form.
        public bool CanJoinPrevious
        {
            get { return Final != NoGlyph; }
        }

        // A letter can connect to the following letter when it has an initial form.
        public bool CanJoinNext
        {
            get { return Initial != NoGlyph; }
        }
    }

    private enum Direction
    {
        Neutral = 0,
        Ltr = 1,
        Rtl = 2
    }

    private struct TextRun
    {
        public Direction Direction;
        public string Text;
    }

    private static readonly Dictionary<char, LetterForms> Forms = BuildForms();

    /// <summary>
    /// Shapes Arabic and converts the string to visual order. Non-Arabic text is
    /// returned unchanged, so this is safe to call on every HUD label.
    /// </summary>
    public static string PrepareForDisplay(string text)
    {
        if (string.IsNullOrEmpty(text) || !ContainsRtl(text))
            return text;

        string shaped = ShapeArabic(text);
        return ReorderToVisual(shaped);
    }

    /// <summary>True when the string contains Arabic/Hebrew characters.</summary>
    public static bool ContainsRtl(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        for (int i = 0; i < text.Length; i++)
        {
            if (IsRtlChar(text[i]))
                return true;
        }

        return false;
    }

    private static string ShapeArabic(string text)
    {
        StringBuilder output = new StringBuilder(text.Length);

        for (int i = 0; i < text.Length; i++)
        {
            char current = text[i];

            // Harakat and other combining marks are transparent to joining.
            if (IsCombiningMark(current))
            {
                output.Append(current);
                continue;
            }

            LetterForms forms;

            if (!Forms.TryGetValue(current, out forms))
            {
                output.Append(current);
                continue;
            }

            int previousIndex = FindPreviousLetterIndex(text, i);
            int nextIndex = FindNextLetterIndex(text, i);

            bool joinsPrevious =
                previousIndex >= 0 &&
                forms.CanJoinPrevious &&
                CanJoinNext(text[previousIndex]);

            // Lam followed by an alef variant becomes a single ligature glyph.
            // It keeps whatever connection the lam had to the letter before it.
            if (current == '\u0644' &&
                nextIndex >= 0 &&
                IsAlefVariant(text[nextIndex]))
            {
                output.Append(GetLamAlefLigature(text[nextIndex], joinsPrevious));
                i = nextIndex;
                continue;
            }

            bool joinsNext =
                nextIndex >= 0 &&
                forms.CanJoinNext &&
                CanJoinPrevious(text[nextIndex]);

            char glyph;

            if (joinsPrevious && joinsNext && forms.Medial != NoGlyph)
                glyph = forms.Medial;
            else if (joinsPrevious && forms.Final != NoGlyph)
                glyph = forms.Final;
            else if (joinsNext && forms.Initial != NoGlyph)
                glyph = forms.Initial;
            else
                glyph = forms.Isolated;

            output.Append(glyph);
        }

        return output.ToString();
    }

    private static int FindPreviousLetterIndex(string text, int index)
    {
        for (int i = index - 1; i >= 0; i--)
        {
            if (IsCombiningMark(text[i]))
                continue;

            return Forms.ContainsKey(text[i]) ? i : -1;
        }

        return -1;
    }

    private static int FindNextLetterIndex(string text, int index)
    {
        for (int i = index + 1; i < text.Length; i++)
        {
            if (IsCombiningMark(text[i]))
                continue;

            return Forms.ContainsKey(text[i]) ? i : -1;
        }

        return -1;
    }

    private static bool CanJoinNext(char character)
    {
        LetterForms forms;
        return Forms.TryGetValue(character, out forms) && forms.CanJoinNext;
    }

    private static bool CanJoinPrevious(char character)
    {
        LetterForms forms;
        return Forms.TryGetValue(character, out forms) && forms.CanJoinPrevious;
    }

    private static bool IsAlefVariant(char character)
    {
        return character == '\u0622' || // alef with madda
               character == '\u0623' || // alef with hamza above
               character == '\u0625' || // alef with hamza below
               character == '\u0627';   // alef
    }

    private static char GetLamAlefLigature(char alef, bool joinsPrevious)
    {
        switch (alef)
        {
            case '\u0622':
                return joinsPrevious ? '\uFEF6' : '\uFEF5';

            case '\u0623':
                return joinsPrevious ? '\uFEF8' : '\uFEF7';

            case '\u0625':
                return joinsPrevious ? '\uFEFA' : '\uFEF9';

            default:
                return joinsPrevious ? '\uFEFC' : '\uFEFB';
        }
    }

    private static string ReorderToVisual(string text)
    {
        Direction baseDirection = FindBaseDirection(text);

        List<TextRun> runs = BuildRuns(text, baseDirection);

        StringBuilder output = new StringBuilder(text.Length);

        if (baseDirection == Direction.Rtl)
        {
            for (int i = runs.Count - 1; i >= 0; i--)
                AppendRun(output, runs[i]);
        }
        else
        {
            for (int i = 0; i < runs.Count; i++)
                AppendRun(output, runs[i]);
        }

        return output.ToString();
    }

    private static void AppendRun(StringBuilder output, TextRun run)
    {
        if (run.Direction == Direction.Rtl)
            AppendReversedClusters(output, run.Text);
        else
            output.Append(run.Text);
    }

    private static Direction FindBaseDirection(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            Direction direction = GetDirection(text[i]);

            if (direction != Direction.Neutral)
                return direction;
        }

        return Direction.Ltr;
    }

    private static List<TextRun> BuildRuns(string text, Direction baseDirection)
    {
        List<TextRun> rawRuns = new List<TextRun>();

        int index = 0;

        while (index < text.Length)
        {
            Direction direction = GetDirection(text[index]);
            int start = index;

            while (index < text.Length && GetDirection(text[index]) == direction)
                index++;

            rawRuns.Add(new TextRun
            {
                Direction = direction,
                Text = text.Substring(start, index - start)
            });
        }

        // Resolve neutral runs (spaces, punctuation) to the surrounding strong
        // direction. Neutral text after a strong run follows that run, which is
        // how a space between an Arabic word and a number behaves in bidi.
        for (int i = 0; i < rawRuns.Count; i++)
        {
            if (rawRuns[i].Direction != Direction.Neutral)
                continue;

            Direction beforeDirection = Direction.Neutral;
            Direction afterDirection = Direction.Neutral;

            for (int before = i - 1; before >= 0; before--)
            {
                if (rawRuns[before].Direction != Direction.Neutral)
                {
                    beforeDirection = rawRuns[before].Direction;
                    break;
                }
            }

            for (int after = i + 1; after < rawRuns.Count; after++)
            {
                if (rawRuns[after].Direction != Direction.Neutral)
                {
                    afterDirection = rawRuns[after].Direction;
                    break;
                }
            }

            // Same direction on both sides -> follow it. Different directions
            // -> follow the paragraph direction, as Unicode's bidi algorithm
            // does. This also prevents a space between a number and an Arabic
            // word from being emitted twice when the runs are reversed.
            Direction resolved;

            if (beforeDirection != Direction.Neutral &&
                afterDirection != Direction.Neutral)
            {
                resolved = beforeDirection == afterDirection
                    ? beforeDirection
                    : baseDirection;
            }
            else if (beforeDirection != Direction.Neutral)
            {
                resolved = beforeDirection;
            }
            else if (afterDirection != Direction.Neutral)
            {
                resolved = afterDirection;
            }
            else
            {
                resolved = baseDirection;
            }

            TextRun resolvedRun = rawRuns[i];
            resolvedRun.Direction = resolved;
            rawRuns[i] = resolvedRun;
        }

        // Merge neighbouring runs that ended up with the same direction.
        List<TextRun> mergedRuns = new List<TextRun>(rawRuns.Count);

        for (int i = 0; i < rawRuns.Count; i++)
        {
            if (mergedRuns.Count > 0 &&
                mergedRuns[mergedRuns.Count - 1].Direction == rawRuns[i].Direction)
            {
                TextRun previous = mergedRuns[mergedRuns.Count - 1];
                previous.Text += rawRuns[i].Text;
                mergedRuns[mergedRuns.Count - 1] = previous;
            }
            else
            {
                mergedRuns.Add(rawRuns[i]);
            }
        }

        return mergedRuns;
    }

    /// <summary>
    /// Reverses a right-to-left run for left-to-right rendering. Base letters
    /// keep their combining marks attached, and mirrored punctuation is swapped.
    /// </summary>
    private static void AppendReversedClusters(StringBuilder output, string text)
    {
        List<string> clusters = new List<string>(text.Length);
        StringBuilder current = null;

        for (int i = 0; i < text.Length; i++)
        {
            char character = text[i];

            if (IsCombiningMark(character) && clusters.Count > 0)
            {
                if (current == null)
                    current = new StringBuilder();

                current.Length = 0;
                current.Append(clusters[clusters.Count - 1]);
                current.Append(character);
                clusters[clusters.Count - 1] = current.ToString();
            }
            else
            {
                clusters.Add(character.ToString());
            }
        }

        for (int i = clusters.Count - 1; i >= 0; i--)
        {
            string cluster = clusters[i];

            if (cluster.Length == 1)
                output.Append(Mirror(cluster[0]));
            else
                output.Append(cluster);
        }
    }

    private static char Mirror(char character)
    {
        switch (character)
        {
            case '(': return ')';
            case ')': return '(';
            case '[': return ']';
            case ']': return '[';
            case '{': return '}';
            case '}': return '{';
            case '<': return '>';
            case '>': return '<';
            case '\u00AB': return '\u00BB'; // «
            case '\u00BB': return '\u00AB'; // »
            default: return character;
        }
    }

    private static Direction GetDirection(char character)
    {
        if (IsRtlChar(character))
            return Direction.Rtl;

        if (char.IsLetterOrDigit(character))
            return Direction.Ltr;

        return Direction.Neutral;
    }

    private static bool IsRtlChar(char character)
    {
        // Arabic-Indic digits are numbers: they stay in logical order inside an
        // otherwise right-to-left line.
        if (char.IsDigit(character))
            return false;

        return (character >= '\u0590' && character <= '\u05FF') || // Hebrew
               (character >= '\u0600' && character <= '\u06FF') || // Arabic
               (character >= '\u0700' && character <= '\u074F') || // Syriac
               (character >= '\u0750' && character <= '\u077F') || // Arabic Supplement
               (character >= '\u0780' && character <= '\u07BF') || // Thaana
               (character >= '\u08A0' && character <= '\u08FF') || // Arabic Extended-A
               (character >= '\uFB1D' && character <= '\uFB4F') || // Hebrew presentation forms
               (character >= '\uFB50' && character <= '\uFDFF') || // Arabic presentation forms-A
               (character >= '\uFE70' && character <= '\uFEFF');   // Arabic presentation forms-B
    }

    private static bool IsCombiningMark(char character)
    {
        switch (CharUnicodeInfo.GetUnicodeCategory(character))
        {
            case UnicodeCategory.NonSpacingMark:
            case UnicodeCategory.SpacingCombiningMark:
            case UnicodeCategory.EnclosingMark:
                return true;
            default:
                return false;
        }
    }

    private static Dictionary<char, LetterForms> BuildForms()
    {
        Dictionary<char, LetterForms> map = new Dictionary<char, LetterForms>();

        // Isolated forms only (letters that never join).
        Add(map, '\u0621', '\uFE80'); // hamza

        // Right-joining letters (connect to the previous letter only).
        Add(map, '\u0622', '\uFE81', '\uFE82'); // alef with madda
        Add(map, '\u0623', '\uFE83', '\uFE84'); // alef with hamza above
        Add(map, '\u0624', '\uFE85', '\uFE86'); // waw with hamza
        Add(map, '\u0625', '\uFE87', '\uFE88'); // alef with hamza below
        Add(map, '\u0627', '\uFE8D', '\uFE8E'); // alef
        Add(map, '\u0629', '\uFE93', '\uFE94'); // teh marbuta
        Add(map, '\u062F', '\uFEA9', '\uFEAA'); // dal
        Add(map, '\u0630', '\uFEAB', '\uFEAC'); // thal
        Add(map, '\u0631', '\uFEAD', '\uFEAE'); // reh
        Add(map, '\u0632', '\uFEAF', '\uFEB0'); // zain
        Add(map, '\u0648', '\uFEED', '\uFEEE'); // waw
        Add(map, '\u0649', '\uFEEF', '\uFEF0'); // alef maksura
        Add(map, '\u0671', '\uFB50', '\uFB51'); // alef wasla
        Add(map, '\u0698', '\uFB8A', '\uFB8B'); // jeh (Persian)
        Add(map, '\u06D2', '\uFBAE', '\uFBAF'); // yeh barree (Urdu)

        // Dual-joining letters (isolated, final, initial, medial).
        Add(map, '\u0626', '\uFE89', '\uFE8A', '\uFE8B', '\uFE8C'); // yeh with hamza
        Add(map, '\u0628', '\uFE8F', '\uFE90', '\uFE91', '\uFE92'); // beh
        Add(map, '\u062A', '\uFE95', '\uFE96', '\uFE97', '\uFE98'); // teh
        Add(map, '\u062B', '\uFE99', '\uFE9A', '\uFE9B', '\uFE9C'); // theh
        Add(map, '\u062C', '\uFE9D', '\uFE9E', '\uFE9F', '\uFEA0'); // jeem
        Add(map, '\u062D', '\uFEA1', '\uFEA2', '\uFEA3', '\uFEA4'); // hah
        Add(map, '\u062E', '\uFEA5', '\uFEA6', '\uFEA7', '\uFEA8'); // khah
        Add(map, '\u0633', '\uFEB1', '\uFEB2', '\uFEB3', '\uFEB4'); // seen
        Add(map, '\u0634', '\uFEB5', '\uFEB6', '\uFEB7', '\uFEB8'); // sheen
        Add(map, '\u0635', '\uFEB9', '\uFEBA', '\uFEBB', '\uFEBC'); // sad
        Add(map, '\u0636', '\uFEBD', '\uFEBE', '\uFEBF', '\uFEC0'); // dad
        Add(map, '\u0637', '\uFEC1', '\uFEC2', '\uFEC3', '\uFEC4'); // tah
        Add(map, '\u0638', '\uFEC5', '\uFEC6', '\uFEC7', '\uFEC8'); // zah
        Add(map, '\u0639', '\uFEC9', '\uFECA', '\uFECB', '\uFECC'); // ain
        Add(map, '\u063A', '\uFECD', '\uFECE', '\uFECF', '\uFED0'); // ghain
        Add(map, '\u0641', '\uFED1', '\uFED2', '\uFED3', '\uFED4'); // feh
        Add(map, '\u0642', '\uFED5', '\uFED6', '\uFED7', '\uFED8'); // qaf
        Add(map, '\u0643', '\uFED9', '\uFEDA', '\uFEDB', '\uFEDC'); // kaf
        Add(map, '\u0644', '\uFEDD', '\uFEDE', '\uFEDF', '\uFEE0'); // lam
        Add(map, '\u0645', '\uFEE1', '\uFEE2', '\uFEE3', '\uFEE4'); // meem
        Add(map, '\u0646', '\uFEE5', '\uFEE6', '\uFEE7', '\uFEE8'); // noon
        Add(map, '\u0647', '\uFEE9', '\uFEEA', '\uFEEB', '\uFEEC'); // heh
        Add(map, '\u064A', '\uFEF1', '\uFEF2', '\uFEF3', '\uFEF4'); // yeh
        Add(map, '\u067E', '\uFB56', '\uFB57', '\uFB58', '\uFB59'); // peh (Persian)
        Add(map, '\u0686', '\uFB7A', '\uFB7B', '\uFB7C', '\uFB7D'); // tcheh (Persian)
        Add(map, '\u06A9', '\uFB8E', '\uFB8F', '\uFB90', '\uFB91'); // keheh (Persian)
        Add(map, '\u06AF', '\uFB92', '\uFB93', '\uFB94', '\uFB95'); // gaf (Persian)
        Add(map, '\u06CC', '\uFBFC', '\uFBFD', '\uFBFE', '\uFBFF'); // farsi yeh

        return map;
    }

    private static void Add(
        Dictionary<char, LetterForms> map,
        char letter,
        char isolated,
        char final = NoGlyph,
        char initial = NoGlyph,
        char medial = NoGlyph)
    {
        map[letter] = new LetterForms
        {
            Isolated = isolated,
            Final = final,
            Initial = initial,
            Medial = medial
        };
    }
}
