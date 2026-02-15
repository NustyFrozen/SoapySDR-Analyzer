namespace SoapyVNACommon;

public interface IWidget
{
    public event EventHandler? OnWidgetExit;
    public event EventHandler? OnWidgetEnter;
    void RenderWidget();
    void WidgetEnter();
    void WidgetExit();
    void InitWidget();
}