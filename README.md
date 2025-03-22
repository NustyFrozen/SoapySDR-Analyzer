# Soapy-SA - A Vendor neutral SDR based Spectrum Analyzer
![Spectrum Image](ui.png)

## motive and goal
make an industry standard spectrum analyzer software
that supports all software defined radio vendors for free
since known SA solutions in the market are quite expensive

## current Features
Swept SA Using Welch's method with:
<br>
- Device - SDR selection, sample rate, gain, LO sleep, IQ correction, Read sensor data
<br>
- Amplitude - offset, leveling, graph range settings
<br>
- Frequency - span, center, start, end
<br>
- marker - 9 markers, delta, peaksearch, band power, mk -> mk diff
<br>
- trace - 6 traces, Modes active view clear, Status normal MaxHold minHold Average
<br>
- Video - FFT-Window, FFT-length, FFT-segments, FFT-overlap, refresh rate, RBW, VBW 

### TODO features
real time SA & trigger to visualize and see bursts like signals
<br>
<br>
support for external programmable gain & attenuators
<br>
<br>
Vector network analyzer (like S11 & S21) implementation with general User based couplers and circulators
<br>
<br>
additional implementation to swept SA like preset, modulation measurement, etc...

## implementation & support
- Windows only, at the moment
- SDR - SoapySDR based using offical swig .NET binding
- Graphical Engine - DirectX back-end with ImGui through ClickableTransparentOverlay

Soapy pre-built modules drivers include uhd, limesdr, hackrf, airspy, rtl-sdr