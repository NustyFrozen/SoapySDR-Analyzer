import win32file
import win32pipe
import time,re,logging,threading
import power
# Python 2 pipe server (ReadThenWrite)
logger = logging.getLogger(__name__)
def resweep(args,device):
    device.sweep(
        args.freq[0], args.freq[1], args.bins, args.repeats,
        runs=args.runs, time_limit=args.elapsed, overlap=args.overlap, crop=args.crop,
        fft_window=args.fft_window, fft_overlap=args.fft_overlap / 100, log_scale=not args.linear,
        remove_dc=args.remove_dc, detrend=args.detrend if args.detrend != 'none' else None,
        lnb_lo=args.lnb_lo, tune_delay=args.tune_delay, reset_stream=args.reset_stream,
        base_buffer_size=args.buffer_size, max_buffer_size=args.max_buffer_size,
        max_threads=args.max_threads, max_queue_size=args.max_queue_size
    )
def processEvent(args,device,event):
    logger.error(event[0])
    if str(event[0]) == 'change_gain':
        logger.info("[SoapyPower] Changing Gain")
        device.device.set_gain(event[1],float(event[2]))
    if str(event[0]) == 'change_frequency':
        min_freq,max_freq = (float(event[1]),float(event[2]))
        device.freq_list = device.freq_plan(min_freq, max_freq,args.bins,args.overlap)
        device._min_freq = min_freq
        device._max_freq = max_freq
        

def startPipe(device,args):
   fileHandle = win32file.CreateFile(
    "\\\\.\\pipe\\SoapySpectrum", 
    win32file.GENERIC_READ | win32file.GENERIC_WRITE, 
    0, 
    None, 
    win32file.OPEN_EXISTING, 
    0, 
    None)
   while not power._shutdown:
        left, data = win32file.ReadFile(fileHandle, 4096)
        data = data.decode('ascii')
        data =  re.search('"(.*)"', data).group(1)
        logger.info("[SoapyPower] received data <-- " + (data))
        processEvent(args,device,str(data).split('@'))
        win32file.WriteFile(fileHandle,"ack".encode(), None)