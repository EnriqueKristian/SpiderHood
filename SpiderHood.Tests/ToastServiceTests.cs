using SpiderHood.Models;
using SpiderHood.Services;

namespace SpiderHood.Tests;

public class ToastServiceTests
{
    [Fact]
    public void ShowSuccess_RaisesOnShowWithSuccessTypeAndDefaultTitle()
    {
        var service = new ToastService();
        ToastMessage? captured = null;
        service.OnShow += msg => captured = msg;

        service.ShowSuccess("Guardado correctamente");

        Assert.NotNull(captured);
        Assert.Equal(ToastType.Success, captured!.Type);
        Assert.Equal("Éxito", captured.Title);
        Assert.Equal("Guardado correctamente", captured.Message);
        Assert.Equal(3000, captured.Duration);
    }

    [Fact]
    public void ShowError_UsesLongerDurationAndCustomTitle()
    {
        var service = new ToastService();
        ToastMessage? captured = null;
        service.OnShow += msg => captured = msg;

        service.ShowError("Algo falló", title: "Ups");

        Assert.Equal(ToastType.Error, captured!.Type);
        Assert.Equal("Ups", captured.Title);
        Assert.Equal(5000, captured.Duration);
    }

    [Fact]
    public void ShowWarning_UsesWarningTypeAndDefaultTitle()
    {
        var service = new ToastService();
        ToastMessage? captured = null;
        service.OnShow += msg => captured = msg;

        service.ShowWarning("Cuidado");

        Assert.Equal(ToastType.Warning, captured!.Type);
        Assert.Equal("Advertencia", captured.Title);
        Assert.Equal(4000, captured.Duration);
    }

    [Fact]
    public void ShowInfo_UsesInfoTypeAndDefaultTitle()
    {
        var service = new ToastService();
        ToastMessage? captured = null;
        service.OnShow += msg => captured = msg;

        service.ShowInfo("Dato");

        Assert.Equal(ToastType.Info, captured!.Type);
        Assert.Equal("Información", captured.Title);
        Assert.Equal(3000, captured.Duration);
    }

    [Fact]
    public void Show_WithNoSubscribers_DoesNotThrow()
    {
        var service = new ToastService();
        var exception = Record.Exception(() => service.ShowSuccess("Nadie escucha"));
        Assert.Null(exception);
    }
}
