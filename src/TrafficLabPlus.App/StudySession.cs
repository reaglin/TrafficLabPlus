using System.IO;
using TrafficLabPlus.Core.Model;

namespace TrafficLabPlus.App;

/// <summary>
/// The study open in the window: the document, where it is saved, whether it has unsaved changes,
/// and undo. Every change goes through <see cref="Edit"/>, which keeps the undo copy, marks the
/// value as typed by the student, and tells the window (the preview rebuilds, the drawing redraws,
/// the problems list is checked again).
/// </summary>
public sealed class StudySession
{
    private readonly Stack<string> _undo = new();
    private readonly Stack<string> _redo = new();
    private string? _lastKey;
    private DateTime _lastEdit;

    public StudySession(StudyDocument document, string? path, bool isExample)
    {
        Document = document;
        Path = path;
        IsExample = isExample;
    }

    public StudyDocument Document { get; }

    public Study Study => Document.Study;

    /// <summary>The file this study is saved in; null until it is saved (a new study, the example).</summary>
    public string? Path { get; private set; }

    /// <summary>The built-in LPGA example, not yet saved as the student's own.</summary>
    public bool IsExample { get; private set; }

    public bool IsDirty { get; private set; }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>Something in the study changed (an edit, an undo).</summary>
    public event EventHandler? Changed;

    /// <summary>The study was put back by undo or redo: anything showing values must read them again.</summary>
    public event EventHandler? Reloaded;

    /// <summary>The name a person reads: the file's name, or the study's title.</summary>
    public string DisplayName =>
        Path is not null ? System.IO.Path.GetFileNameWithoutExtension(Path)
        : IsExample ? Study.Title + " (example)"
        : string.IsNullOrWhiteSpace(Study.Title) ? "New study" : Study.Title;

    /// <summary>Makes one change. <paramref name="key"/> names the value (<c>node:I1/cycle</c>): it
    /// is marked as typed, and quick changes to the same value (typing, dragging) undo as one.</summary>
    public void Edit(string? key, Action change)
    {
        Snapshot(key);
        change();
        if (key is not null)
        {
            StudyEdits.Mark(Study, key, Origins.Typed);
        }

        IsDirty = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A change that is shown as it happens but told to the window when it ends (a drag).</summary>
    public void BeginQuiet(string key) => Snapshot(key);

    public void EndQuiet(string key)
    {
        StudyEdits.Mark(Study, key, Origins.Typed);
        IsDirty = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A study that exists only in the window so far (new, or opened from a .json file).</summary>
    public void MarkUnsaved() => IsDirty = true;

    public void Undo() => Swap(_undo, _redo);

    public void Redo() => Swap(_redo, _undo);

    /// <exception cref="IOException">The file could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder cannot be written to.</exception>
    public void SaveAs(string path)
    {
        StudyFile.Save(Document, path);
        Path = path;
        IsExample = false;
        IsDirty = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Snapshot(string? key)
    {
        DateTime now = DateTime.UtcNow;
        bool sameRun = key is not null && key == _lastKey && now - _lastEdit < TimeSpan.FromSeconds(2);
        _lastKey = key;
        _lastEdit = now;
        if (sameRun)
        {
            return;
        }

        _undo.Push(StudyJson.Write(Study));
        _redo.Clear();
    }

    private void Swap(Stack<string> from, Stack<string> to)
    {
        if (from.Count == 0)
        {
            return;
        }

        to.Push(StudyJson.Write(Study));
        Document.Study = StudyJson.Read(from.Pop());
        _lastKey = null;
        IsDirty = true;
        Reloaded?.Invoke(this, EventArgs.Empty);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
