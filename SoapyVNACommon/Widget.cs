namespace SoapyVNACommon;

public interface IWidget
{
    void RenderWidget();

    void ReleaseSdr();

    void HandleSdr();

    void InitWidget();
}