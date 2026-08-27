using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace EnglishLeitner.WebClient.Components;

public partial class GeneralDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public string Icon { get; set; } = Icons.Material.Outlined.QuestionMark;

    [Parameter]
    public Color Color { get; set; } = Color.Default;

    [Parameter]
    public string? Title { get; set; }

    [Parameter]
    public RenderFragment? Content { get; set; }

    private void Submit() => MudDialog?.Close(DialogResult.Ok(true));

    private void Cancel() => MudDialog?.Cancel();
}
