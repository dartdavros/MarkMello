using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Infrastructure.Markdown;
using MarkMello.Presentation.Localization;
using MarkMello.Presentation.ViewModels;
using MarkMello.Presentation.Views;
using MarkMello.Presentation.Views.Markdown;

namespace MarkMello.Presentation.Tests;

/// <summary>
/// Реальный сценарий инлайн-правки в режиме чтения: клик открывает редактор, текст набирается
/// с клавиатуры и НЕ коммитится, после чего сразу жмётся горячая клавиша. Используются
/// настоящие <see cref="ViewerView"/>, <see cref="MarkdownDocumentView"/>, контроллер и
/// <see cref="MarkdownQuickEditorControl"/>; горячие клавиши идут через <c>Window.KeyBindings</c>,
/// как в <see cref="MainWindow"/>.
/// </summary>
[Collection(AvaloniaHeadlessTestGroup.Name)]
public sealed class QuickInlineEditingScenarioTests
{
    private const string FirstContent = "First paragraph of the document.\n\nSecond paragraph.\n";
    private const string SecondContent = "Another file.\n";
    private const string TypedText = "XYZ ";

    private readonly AvaloniaHeadlessFixture _fixture;

    public QuickInlineEditingScenarioTests(AvaloniaHeadlessFixture fixture) => _fixture = fixture;

    [Fact]
    public Task CtrlSWithUncommittedInlineInputSavesTypedText()
    {
        return _fixture.RunAsync(async () =>
        {
            var scenario = await Scenario.CreateAsync();
            var editor = await scenario.StartInlineEditAndTypeAsync(TypedText);

            scenario.Window.KeyPress(Key.S, RawInputModifiers.Control, PhysicalKey.S, "s");
            await Scenario.SettleAsync();

            var save = Assert.Single(scenario.Saver.Saves);
            Assert.Equal(scenario.FirstPath, save.Path);
            Assert.Equal(TypedText + FirstContent, save.Content);
            Assert.False(scenario.ViewModel.IsDirty);
            Assert.Null(scenario.FindActiveEditor());
            Assert.NotNull(editor);
        });
    }

    [Fact]
    public Task CtrlTabWithUncommittedInlineInputKeepsTypedTextInItsTab()
    {
        return _fixture.RunAsync(async () =>
        {
            var scenario = await Scenario.CreateAsync(openSecondTab: true);
            var firstTab = scenario.ViewModel.OpenDocuments.ActiveTab!;
            await scenario.StartInlineEditAndTypeAsync(TypedText);

            scenario.Window.KeyPress(Key.Tab, RawInputModifiers.Control, PhysicalKey.Tab, "\t");
            await Scenario.SettleAsync();

            Assert.NotSame(firstTab, scenario.ViewModel.OpenDocuments.ActiveTab);
            Assert.Equal(SecondContent, scenario.ViewModel.Document!.Content);
            Assert.True(firstTab.IsDirty);
            Assert.Equal(TypedText + FirstContent, firstTab.Document!.Content);
        });
    }

    [Fact]
    public Task CtrlEWithUncommittedInlineInputOpensFullEditorWithTypedText()
    {
        return _fixture.RunAsync(async () =>
        {
            var scenario = await Scenario.CreateAsync();
            await scenario.StartInlineEditAndTypeAsync(TypedText);

            scenario.Window.KeyPress(Key.E, RawInputModifiers.Control, PhysicalKey.E, "e");
            await Scenario.SettleAsync();

            Assert.True(scenario.ViewModel.IsEditMode);
            Assert.Equal(TypedText + FirstContent, scenario.ViewModel.EditorSession!.SourceText);
            Assert.True(scenario.ViewModel.IsDirty);
        });
    }

    [Fact]
    public Task CtrlWWithUncommittedInlineInputAsksAboutUnsavedChanges()
    {
        return _fixture.RunAsync(async () =>
        {
            var scenario = await Scenario.CreateAsync();
            await scenario.StartInlineEditAndTypeAsync(TypedText);

            scenario.Window.KeyPress(Key.W, RawInputModifiers.Control, PhysicalKey.W, "w");
            await Scenario.SettleAsync();

            Assert.True(scenario.ViewModel.IsDirtyPromptOpen);
            Assert.Equal(TypedText + FirstContent, scenario.ViewModel.EditorSession!.SourceText);
        });
    }

    [Fact]
    public Task ReloadWithUncommittedInlineInputAsksAboutUnsavedChanges()
    {
        return _fixture.RunAsync(async () =>
        {
            var scenario = await Scenario.CreateAsync();
            await scenario.StartInlineEditAndTypeAsync(TypedText);

            scenario.Window.KeyPress(Key.F5, RawInputModifiers.None, PhysicalKey.F5, null);
            await Scenario.SettleAsync();

            Assert.True(scenario.ViewModel.IsDirtyPromptOpen);
            Assert.Equal(TypedText + FirstContent, scenario.ViewModel.EditorSession!.SourceText);
        });
    }

    [Fact]
    public Task OpeningAnotherFileWithUncommittedInlineInputKeepsTypedTextInItsTab()
    {
        return _fixture.RunAsync(async () =>
        {
            var scenario = await Scenario.CreateAsync();
            var firstTab = scenario.ViewModel.OpenDocuments.ActiveTab!;
            await scenario.StartInlineEditAndTypeAsync(TypedText);

            await scenario.ViewModel.OpenPathAsync(scenario.SecondPath);
            await Scenario.SettleAsync();

            Assert.Equal(SecondContent, scenario.ViewModel.Document!.Content);
            Assert.Equal(TypedText + FirstContent, firstTab.Document!.Content);
            Assert.True(firstTab.IsDirty);
        });
    }

    [Fact]
    public Task DoubleClickSelectsWordAndDoesNotOpenInlineEditor()
    {
        return _fixture.RunAsync(async () =>
        {
            var scenario = await Scenario.CreateAsync();
            var point = scenario.GetPointInsideFirstParagraph();

            scenario.Window.MouseDown(point, MouseButton.Left);
            scenario.Window.MouseUp(point, MouseButton.Left);
            scenario.Window.MouseDown(point, MouseButton.Left);
            scenario.Window.MouseUp(point, MouseButton.Left);
            await Task.Delay(400).ConfigureAwait(true);
            await Scenario.SettleAsync();

            Assert.Null(scenario.FindActiveEditor());
            Assert.True(scenario.DocumentView.HasSelection);
            Assert.DoesNotContain(' ', scenario.DocumentView.SelectedText.Trim());
        });
    }

    [Fact]
    public Task StaleInlineEditIsNotAppliedToAnotherSource()
    {
        return _fixture.RunAsync(async () =>
        {
            var scenario = await Scenario.CreateAsync();
            var editor = await scenario.StartInlineEditAndTypeAsync(TypedText);
            var edits = new List<MarkdownDocumentContentEditedEventArgs>();
            scenario.DocumentView.DocumentContentEdited += (_, e) => edits.Add(e);

            // Исходник подменился в обход команд (как при гонке привязок): старые смещения
            // к нему не относятся, правку нужно отбросить, а не наложить на чужой текст.
            scenario.DocumentView.SourceText = "Completely different text.\n";
            editor.Commit();
            await Scenario.SettleAsync();

            Assert.Empty(edits);
            Assert.Null(scenario.FindActiveEditor());
        });
    }

    private sealed class Scenario
    {
        private Scenario(Window window, ViewerView viewer, ShellViewModel viewModel, RecordingDocumentSaver saver, string firstPath, string secondPath)
        {
            Window = window;
            Viewer = viewer;
            ViewModel = viewModel;
            Saver = saver;
            FirstPath = firstPath;
            SecondPath = secondPath;
        }

        public Window Window { get; }

        public ViewerView Viewer { get; }

        public ShellViewModel ViewModel { get; }

        public RecordingDocumentSaver Saver { get; }

        public string FirstPath { get; }

        public string SecondPath { get; }

        public MarkdownDocumentView DocumentView
            => Viewer.GetVisualDescendants().OfType<MarkdownDocumentView>().Single();

        public static async Task<Scenario> CreateAsync(bool openSecondTab = false)
        {
            var directory = Path.Combine(Path.GetTempPath(), "MarkMello.Tests", "quick-inline");
            var firstPath = Path.Combine(directory, "first.md");
            var secondPath = Path.Combine(directory, "second.md");

            var loader = new StubDocumentLoader();
            loader.Sources[firstPath] = new MarkdownSource(firstPath, "first.md", FirstContent);
            loader.Sources[secondPath] = new MarkdownSource(secondPath, "second.md", SecondContent);
            var saver = new RecordingDocumentSaver();
            var fileSystem = new FakeWorkspaceFileSystem();

            var viewModel = new ShellViewModel(
                new OpenDocumentUseCase(loader),
                new SaveDocumentUseCase(saver),
                new StubFilePicker(),
                new StubCommandLineActivation(),
                new LocalizationService(AppLanguage.English),
                new InMemorySettingsStore(),
                new RecordingThemeService(),
                new RecordingStartupMetrics(),
                new RenderMarkdownDocumentUseCase(new MarkdigMarkdownDocumentRenderer(), new FakeDiagramRenderService()),
                new StubUpdateService(),
                new OpenFolderUseCase(fileSystem),
                new ExpandFolderNodeUseCase(fileSystem),
                new SearchWorkspaceFilesUseCase(fileSystem),
                new WorkspaceFileOperationsUseCase(fileSystem, new FakePlatformServices()),
                new FakePlatformServices(),
                static () => new FakeWorkspaceWatcher(),
                new RecordingWindowLauncher());

            if (openSecondTab)
            {
                await viewModel.OpenPathAsync(secondPath).ConfigureAwait(true);
            }

            await viewModel.OpenPathAsync(firstPath).ConfigureAwait(true);

            var viewer = new ViewerView { DataContext = viewModel };
            var window = new Window { Width = 900, Height = 700, Content = viewer, DataContext = viewModel };
            AddKeyBinding(window, Key.S, viewModel.SaveCommand);
            AddKeyBinding(window, Key.Tab, viewModel.ActivateNextTabCommand);
            AddKeyBinding(window, Key.E, viewModel.ToggleEditModeCommand);
            AddKeyBinding(window, Key.W, viewModel.CloseActiveTabCommand);
            window.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.F5), Command = viewModel.ReloadCommand });
            window.Show();
            await SettleAsync().ConfigureAwait(true);

            return new Scenario(window, viewer, viewModel, saver, firstPath, secondPath);
        }

        public static async Task SettleAsync()
        {
            for (var i = 0; i < 5; i++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Yield();
            }

            Dispatcher.UIThread.RunJobs();
        }

        public MarkdownQuickEditorControl? FindActiveEditor()
            => DocumentView.GetVisualDescendants().OfType<MarkdownQuickEditorControl>().SingleOrDefault();

        public Point GetPointInsideFirstParagraph()
        {
            var fragment = DocumentView.GetVisualDescendants()
                .OfType<MarkdownSelectionTextFragment>()
                .OrderBy(f => f.TranslatePoint(default, Window)?.Y ?? double.MaxValue)
                .First();
            var local = new Point(Math.Min(20, fragment.Bounds.Width / 2), fragment.Bounds.Height / 2);
            return fragment.TranslatePoint(local, Window) ?? throw new InvalidOperationException("Fragment is not in the window.");
        }

        public async Task<MarkdownQuickEditorControl> StartInlineEditAndTypeAsync(string text)
        {
            var point = GetPointInsideFirstParagraph();
            Window.MouseDown(point, MouseButton.Left);
            Window.MouseUp(point, MouseButton.Left);

            // Редактор открывается по таймеру, чтобы второй клик успел стать двойным.
            await Task.Delay(400).ConfigureAwait(true);
            await SettleAsync().ConfigureAwait(true);

            var editor = FindActiveEditor() ?? throw new InvalidOperationException("Inline editor did not open.");
            editor.Focus();
            editor.SetInitialCaret(0);
            Window.KeyTextInput(text);
            await SettleAsync().ConfigureAwait(true);

            Assert.StartsWith(text, editor.Text, StringComparison.Ordinal);
            // Ничего ещё не закоммичено: документ прежний.
            Assert.Equal(FirstContent, ViewModel.Document!.Content);
            return editor;
        }

        private static void AddKeyBinding(Window window, Key key, System.Windows.Input.ICommand command)
            => window.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(key, KeyModifiers.Control), Command = command });
    }
}

public sealed class QuickInlineEditSnapshotTests
{
    private static readonly RenderedMarkdownDocument SomeDocument = RenderedMarkdownDocument.PlainText("x");

    [Fact]
    public void SameSourceIsCurrent()
        => Assert.True(MarkdownQuickInlineEditingController.IsSnapshotStillCurrent(
            "a\nb", "a\nb", SomeDocument, null, 2, 3, "b"));

    [Fact]
    public void SameDocumentWithUntouchedBlockIsCurrent()
        => Assert.True(MarkdownQuickInlineEditingController.IsSnapshotStillCurrent(
            "a\nb\nc!", "a\nb\nc", SomeDocument, SomeDocument, 2, 3, "b"));

    [Fact]
    public void AnotherDocumentWithDifferentSourceIsStale()
        => Assert.False(MarkdownQuickInlineEditingController.IsSnapshotStillCurrent(
            "a\nb\nc!", "a\nb\nc", RenderedMarkdownDocument.PlainText("y"), SomeDocument, 2, 3, "b"));

    [Fact]
    public void ChangedBlockIsStale()
        => Assert.False(MarkdownQuickInlineEditingController.IsSnapshotStillCurrent(
            "a\nZ\nc", "a\nb\nc", SomeDocument, SomeDocument, 2, 3, "b"));

    [Fact]
    public void OffsetsOutsideSourceAreStale()
        => Assert.False(MarkdownQuickInlineEditingController.IsSnapshotStillCurrent(
            "a", "a\nb\nc", SomeDocument, SomeDocument, 2, 3, "b"));
}
