# SoapySDR Analyzer - A Vendor neutral SDR based RF Signal Analyzer


## showcase Videos



https://github.com/user-attachments/assets/f6f64b46-d350-4405-b3f5-7899abffb9f7




https://github.com/user-attachments/assets/5ed95bb9-727f-49ad-a29b-f17a7710cad9


![Spectrum Image](https://github.com/NustyFrozen/Soapy-.NET-Spectrum-Analayzer/blob/main/Media/ui.png?raw=true)

## agenda
Make an industry-level SDR Based RF testing equipments
that supports all software-defined radio vendors for free
since known solutions in the market are quite expensive

## Current Features
RL - Return Loss / VSWR / Reflection Coefficent / Mismatch loss using a circulator and full duplex SDR board
<br>
Swept Spectrum Analyzer

### Signal Analyzer Mode
| Feature | Description 
| ----------- | ----------- |
| Device | SDR selection, sample rate, gain, LO sleep, IQ correction, Read sensor data 
| Amplitude | offset, leveling, graph range settings 
| Frequency | span, center, start, end 
| marker | delta, peaksearch, band power, mk -> mk diff 
| trace | Modes active view clear, Status normal MaxHold minHold Average
| Video | FFT-Window, FFT-length, FFT-segments, FFT-overlap, refresh rate, RBW, VBW
| Calibration | calibrate sdr gain elements and the sdr itself using  external signal source|
| NF Measurement| Using a noise source and an enr table test Active DUT's NF
|Source| given specified tx front-end on a full duplex sdr you can transmit either a CW or tracking signal
|Filter Measurement| gives automatically Detected BW of a filter on the trace given peakvalue -3dB sidelobes
|Channel Measurement| gives on a specified BW the channel characterization like BW power,OBW, PAPR,power density,etc..
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
an [example video](https://github.com/NustyFrozen/Soapy-.NET-Spectrum-Analayzer/blob/main/Media/RL%20test%20demo.mp4?raw=true) how it is measured is included use a circulator or a coupler , in case you do use a coupler please make sure you know what gain values to put before transmitting as the transmission will be reflected to the transmission port and may harm your sdr
