// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Text;

namespace osu.Game.API.UI.Scripting.Editor
{
    /// <summary>
    /// Pure text/cursor model backing <see cref="ScriptEditor"/>. Knows nothing about rendering.
    /// </summary>
    public class ScriptDocument
    {
        private const int max_history = 250;
        private const int indent_size = 4;

        private readonly List<string> lines = new List<string> { string.Empty };
        private readonly List<Snapshot> undoHistory = new List<Snapshot>();
        private readonly List<Snapshot> redoHistory = new List<Snapshot>();

        public IReadOnlyList<string> Lines => lines;

        public int LineCount => lines.Count;

        public int CaretLine { get; private set; }

        public int CaretColumn { get; private set; }

        public int AnchorLine { get; private set; }

        public int AnchorColumn { get; private set; }

        public bool HasSelection => CaretLine != AnchorLine || CaretColumn != AnchorColumn;

        public string Text => string.Join("\n", lines);

        public bool CanUndo => undoHistory.Count > 0;

        public bool CanRedo => redoHistory.Count > 0;

        public void SetText(string text)
        {
            applyText(text);
            CaretLine = lines.Count - 1;
            CaretColumn = lines[lines.Count - 1].Length;
            ClearSelection();
            undoHistory.Clear();
            redoHistory.Clear();
        }

        public void SetCaret(int line, int column, bool extendSelection)
        {
            CaretLine = line;
            CaretColumn = column;
            clampCaret();

            if (!extendSelection)
                ClearSelection();
        }

        public void SelectAll()
        {
            AnchorLine = 0;
            AnchorColumn = 0;
            CaretLine = lines.Count - 1;
            CaretColumn = lines[lines.Count - 1].Length;
        }

        public void ClearSelection()
        {
            AnchorLine = CaretLine;
            AnchorColumn = CaretColumn;
        }

        public void Insert(string text)
        {
            PushUndo();

            if (HasSelection)
                deleteSelectionInternal();

            string[] parts = normalise(text).Split('\n');
            string current = lines[CaretLine];
            string before = current.Substring(0, CaretColumn);
            string after = current.Substring(CaretColumn);

            if (parts.Length == 1)
            {
                lines[CaretLine] = before + parts[0] + after;
                CaretColumn = before.Length + parts[0].Length;
            }
            else
            {
                lines[CaretLine] = before + parts[0];

                int insertAt = CaretLine + 1;

                for (int i = 1; i < parts.Length; i++)
                    lines.Insert(insertAt++, parts[i]);

                int lastLine = CaretLine + parts.Length - 1;
                lines[lastLine] += after;
                CaretLine = lastLine;
                CaretColumn = parts[parts.Length - 1].Length;
            }

            ClearSelection();
        }

        public void InsertLineBreak()
        {
            PushUndo();

            if (HasSelection)
                deleteSelectionInternal();

            string current = lines[CaretLine];

            int indentEnd = 0;

            while (indentEnd < current.Length && (current[indentEnd] == ' ' || current[indentEnd] == '\t'))
                indentEnd++;

            string indent = current.Substring(0, indentEnd);
            string trimmed = current.TrimEnd();
            bool openBracket = trimmed.Length > 0 && (trimmed[trimmed.Length - 1] == '{' || trimmed[trimmed.Length - 1] == '(' || trimmed[trimmed.Length - 1] == '[');

            string insertion = "\n" + indent + (openBracket ? new string(' ', indent_size) : string.Empty);

            Insert(insertion);
        }

        public void Indent(bool shift)
        {
            PushUndo();

            if (HasSelection && selectionSpansMultipleLines())
            {
                getOrderedSelection(out int startLine, out _, out int endLine, out _);

                for (int i = startLine; i <= endLine; i++)
                {
                    if (shift)
                    {
                        int remove = 0;

                        while (remove < indent_size && remove < lines[i].Length && lines[i][remove] == ' ')
                            remove++;

                        lines[i] = lines[i].Substring(remove);
                    }
                    else
                        lines[i] = new string(' ', indent_size) + lines[i];
                }

                clampCaret();
                AnchorLine = Math.Clamp(AnchorLine, 0, lines.Count - 1);
                AnchorColumn = Math.Clamp(AnchorColumn, 0, lines[AnchorLine].Length);
                return;
            }

            if (shift)
            {
                int remove = 0;

                while (remove < indent_size && remove < CaretColumn && lines[CaretLine][CaretColumn - remove - 1] == ' ')
                    remove++;

                lines[CaretLine] = lines[CaretLine].Remove(CaretColumn - remove, remove);
                CaretColumn -= remove;
                ClearSelection();
            }
            else
            {
                lines[CaretLine] = lines[CaretLine].Insert(CaretColumn, new string(' ', indent_size));
                CaretColumn += indent_size;
                ClearSelection();
            }
        }

        public void Backspace()
        {
            PushUndo();

            if (HasSelection)
            {
                deleteSelectionInternal();
                ClearSelection();
                return;
            }

            if (CaretColumn > 0)
            {
                lines[CaretLine] = lines[CaretLine].Remove(CaretColumn - 1, 1);
                CaretColumn--;
            }
            else if (CaretLine > 0)
            {
                int previousLength = lines[CaretLine - 1].Length;
                lines[CaretLine - 1] += lines[CaretLine];
                lines.RemoveAt(CaretLine);
                CaretLine--;
                CaretColumn = previousLength;
            }

            ClearSelection();
        }

        public void DeleteForward()
        {
            PushUndo();

            if (HasSelection)
            {
                deleteSelectionInternal();
                ClearSelection();
                return;
            }

            if (CaretColumn < lines[CaretLine].Length)
                lines[CaretLine] = lines[CaretLine].Remove(CaretColumn, 1);
            else if (CaretLine < lines.Count - 1)
            {
                lines[CaretLine] += lines[CaretLine + 1];
                lines.RemoveAt(CaretLine + 1);
            }

            ClearSelection();
        }

        public void MoveLeft(bool extendSelection)
        {
            if (!extendSelection && HasSelection)
            {
                getOrderedSelection(out int line, out int column, out _, out _);
                CaretLine = line;
                CaretColumn = column;
                ClearSelection();
                clampCaret();
                return;
            }

            if (CaretColumn > 0)
                CaretColumn--;
            else if (CaretLine > 0)
            {
                CaretLine--;
                CaretColumn = lines[CaretLine].Length;
            }

            if (!extendSelection)
                ClearSelection();
        }

        public void MoveRight(bool extendSelection)
        {
            if (!extendSelection && HasSelection)
            {
                getOrderedSelection(out _, out _, out int line, out int column);
                CaretLine = line;
                CaretColumn = column;
                ClearSelection();
                clampCaret();
                return;
            }

            if (CaretColumn < lines[CaretLine].Length)
                CaretColumn++;
            else if (CaretLine < lines.Count - 1)
            {
                CaretLine++;
                CaretColumn = 0;
            }

            if (!extendSelection)
                ClearSelection();
        }

        public void MoveUp(bool extendSelection, int count = 1)
        {
            CaretLine = Math.Max(0, CaretLine - count);
            clampCaret();

            if (!extendSelection)
                ClearSelection();
        }

        public void MoveDown(bool extendSelection, int count = 1)
        {
            CaretLine = Math.Min(lines.Count - 1, CaretLine + count);
            clampCaret();

            if (!extendSelection)
                ClearSelection();
        }

        public void MoveToLineStart(bool extendSelection)
        {
            CaretColumn = 0;

            if (!extendSelection)
                ClearSelection();
        }

        public void MoveToLineEnd(bool extendSelection)
        {
            CaretColumn = lines[CaretLine].Length;

            if (!extendSelection)
                ClearSelection();
        }

        public void MoveToDocumentStart(bool extendSelection)
        {
            CaretLine = 0;
            CaretColumn = 0;

            if (!extendSelection)
                ClearSelection();
        }

        public void MoveToDocumentEnd(bool extendSelection)
        {
            CaretLine = lines.Count - 1;
            CaretColumn = lines[CaretLine].Length;

            if (!extendSelection)
                ClearSelection();
        }

        public string GetSelectedText()
        {
            if (!HasSelection)
                return string.Empty;

            getOrderedSelection(out int startLine, out int startColumn, out int endLine, out int endColumn);

            if (startLine == endLine)
                return lines[startLine].Substring(startColumn, endColumn - startColumn);

            var builder = new StringBuilder();
            builder.Append(lines[startLine].Substring(startColumn));
            builder.Append('\n');

            for (int i = startLine + 1; i < endLine; i++)
            {
                builder.Append(lines[i]);
                builder.Append('\n');
            }

            builder.Append(lines[endLine].Substring(0, endColumn));
            return builder.ToString();
        }

        /// <summary>
        /// Returns the selection as inclusive per-line ranges from the first selected line to the last.
        /// </summary>
        public List<(int line, int start, int end)> GetSelectionRanges()
        {
            var ranges = new List<(int line, int start, int end)>();

            if (!HasSelection)
                return ranges;

            getOrderedSelection(out int startLine, out int startColumn, out int endLine, out int endColumn);

            for (int line = startLine; line <= endLine; line++)
            {
                int length = lines[line].Length;
                int start = line == startLine ? startColumn : 0;
                int end = line == endLine ? endColumn : length;

                if (end < start)
                    end = start;

                ranges.Add((line, start, end));
            }

            return ranges;
        }

        public void PushUndo()
        {
            undoHistory.Add(Capture());

            if (undoHistory.Count > max_history)
                undoHistory.RemoveAt(0);

            redoHistory.Clear();
        }

        public void Undo()
        {
            if (undoHistory.Count == 0)
                return;

            redoHistory.Add(Capture());
            restore(undoHistory[undoHistory.Count - 1]);
            undoHistory.RemoveAt(undoHistory.Count - 1);
        }

        public void Redo()
        {
            if (redoHistory.Count == 0)
                return;

            undoHistory.Add(Capture());
            restore(redoHistory[redoHistory.Count - 1]);
            redoHistory.RemoveAt(redoHistory.Count - 1);
        }

        private void deleteSelectionInternal()
        {
            getOrderedSelection(out int startLine, out int startColumn, out int endLine, out int endColumn);

            if (startLine == endLine)
                lines[startLine] = lines[startLine].Remove(startColumn, endColumn - startColumn);
            else
            {
                lines[startLine] = lines[startLine].Substring(0, startColumn) + lines[endLine].Substring(endColumn);
                lines.RemoveRange(startLine + 1, endLine - startLine);
            }

            CaretLine = startLine;
            CaretColumn = startColumn;
        }

        private bool selectionSpansMultipleLines()
        {
            getOrderedSelection(out int startLine, out _, out int endLine, out _);
            return startLine != endLine;
        }

        private void getOrderedSelection(out int startLine, out int startColumn, out int endLine, out int endColumn)
        {
            int caretIndex = IndexOf(CaretLine, CaretColumn);
            int anchorIndex = IndexOf(AnchorLine, AnchorColumn);

            if (caretIndex <= anchorIndex)
            {
                startLine = CaretLine;
                startColumn = CaretColumn;
                endLine = AnchorLine;
                endColumn = AnchorColumn;
            }
            else
            {
                startLine = AnchorLine;
                startColumn = AnchorColumn;
                endLine = CaretLine;
                endColumn = CaretColumn;
            }
        }

        private int IndexOf(int line, int column)
        {
            int index = 0;

            for (int i = 0; i < line && i < lines.Count; i++)
                index += lines[i].Length + 1;

            return index + column;
        }

        private void clampCaret()
        {
            CaretLine = Math.Clamp(CaretLine, 0, lines.Count - 1);
            CaretColumn = Math.Clamp(CaretColumn, 0, lines[CaretLine].Length);
        }

        private void applyText(string text)
        {
            lines.Clear();
            lines.AddRange(normalise(text).Split('\n'));

            if (lines.Count == 0)
                lines.Add(string.Empty);
        }

        private void restore(Snapshot snapshot)
        {
            applyText(snapshot.Text);
            CaretLine = snapshot.CaretLine;
            CaretColumn = snapshot.CaretColumn;
            AnchorLine = snapshot.AnchorLine;
            AnchorColumn = snapshot.AnchorColumn;
            clampCaret();
        }

        private Snapshot Capture() => new Snapshot(Text, CaretLine, CaretColumn, AnchorLine, AnchorColumn);

        private static string normalise(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

        private readonly struct Snapshot
        {
            public readonly string Text;
            public readonly int CaretLine;
            public readonly int CaretColumn;
            public readonly int AnchorLine;
            public readonly int AnchorColumn;

            public Snapshot(string text, int caretLine, int caretColumn, int anchorLine, int anchorColumn)
            {
                Text = text;
                CaretLine = caretLine;
                CaretColumn = caretColumn;
                AnchorLine = anchorLine;
                AnchorColumn = anchorColumn;
            }
        }
    }
}
