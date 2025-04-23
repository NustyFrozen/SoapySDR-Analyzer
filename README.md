# SoapySDR Analyzer - A Vendor neutral SDR based unoffical RF Equipment testing suite
![Spectrum Image](https://github.com/NustyFrozen/Soapy-.NET-Spectrum-Analayzer/blob/main/Media/ui.png?raw=true)

## Motive and goal
Make an industry-level SDR Based RF testing equipments
that supports all software-defined radio vendors for free
since known solutions in the market are quite expensive

## Current Features
RL - Return Loss / VSWR / Reflection Coefficent / Mismatch loss using a circulator and full duplex SDR board
<br>
Swept Spectrum Analyzer
| Feature | Description | Return Loss | Spectrum Analyzer|
| ----------- | ----------- | ----------- | ----------- |
| Device | SDR selection, sample rate, gain, LO sleep, IQ correction, Read sensor data |✅|✅
| Amplitude | offset, leveling, graph range settings |❌|✅|
| Frequency | span, center, start, end |❌|✅|
| marker | delta, peaksearch, band power, mk -> mk diff |✅|✅|
| trace | Modes active view clear, Status normal MaxHold minHold Average |❌|✅|
| Video | FFT-Window, FFT-length, FFT-segments, FFT-overlap, refresh rate, RBW, VBW  |❌|✅|
| Calibration | calibrate sdr gain elements and the sdr itself using  external signal source |✅|✅|

### TODO features
real-time SA & trigger to visualize and see bursts like signals
<br>
<br>
support for external programmable gain & attenuators
<br>
<br>
<br>
Additional implementation to swept SA, such as preset, modulation measurement, etc.

## implementation & support
- Windows only, at the moment
- SDR - SoapySDR based using offical swig .NET binding
- Graphical Engine - DirectX back-end with ImGui through ClickableTransparentOverlay

Soapy pre-built modules drivers include uhd, limesdr, hackrf, airspy, rtl-sdr

## installation & usage
requirements:
- .NET 8.0
- DirectX installed
- appropriate drivers for your sdr (example: USRP install uhd)
- If the compiled binaries version don't include your SDR simply compile the soapySupport (for example, uhd: https://github.com/pothosware/SoapyUHD) and put the Main dll in SoapySDR\root\SoapySDR\lib\SoapySDR\modules0.8-3, any additional dlls can be added to SoapySDR\Libs
download the compiled binaries from Release and run it, to minimize click insert
### how to calibrate spectrum analyzer
You need an external rf CW signal generator, Select the range you want to calibrate for and step size and transmit the instructed frequency & power, it will iterate all over the required steps and click enter when you see the signal on the FFT-plot
After calibration is done restart the program, and you'll be able to select the calibration.
### how to use return loss / VSWR

**` NOTICE`**` before measuring please note it is illegal to do this test outside a controlled environment & without a proper qualification as you are transmitting white noise over a large frequency range please check your country's regulations before doing so, I am not responsible for any illegal activity you may do`<BR>
what you need is a coupler to measure return loss
an [example video](https://github.com/NustyFrozen/Soapy-.NET-Spectrum-Analayzer/blob/main/Media/RL%20test%20demo.mp4?raw=true) how it is measured is included i Highly recommend to not use a coupler and use a circulator as a coupler will actually return it to the transmit port and please make sure you know what gain values to put before transmitting as the transmission power can destory your sdr
