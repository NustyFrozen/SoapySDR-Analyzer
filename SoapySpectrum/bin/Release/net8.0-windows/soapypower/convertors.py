import re

def float_with_multiplier(self,string):
    global re_float_with_multiplier
    global multipliers
    global re_float_with_multiplier_negative
    """Convert string with optional k, M, G, T multiplier to float"""
    match = re_float_with_multiplier.search(string)
    if not match or not match.group('num'):
        raise ValueError('String "{}" is not numeric!'.format(string))

    num = float(match.group('num'))
    multi = match.group('multi')
    if multi:
        try:
            num *= multipliers[multi]
        except KeyError:
            raise ValueError('Unknown multiplier: {}'.format(multi))
    return num
def freq_or_freq_range(self,string):
    global re_float_with_multiplier
    global multipliers
    global re_float_with_multiplier_negative
    """Convert string with freq. or freq. range to list of floats"""
    return [self.float_with_multiplier(f) for f in string.split(':')]


def specific_gains(self,string):
    """Convert string with gains of individual amplification elements to dict"""
    if not string:
        return {}

    gains = {}
    for gain in string.split(','):
        amp_name, value = gain.split('=')
        gains[amp_name.strip()] = float(value.strip())
    return gains


def device_settings(self,string):
    """Convert string with SoapySDR device settings to dict"""
    if not string:
        return {}

    settings = {}
    for setting in string.split(','):
        setting_name, value = setting.split('=')
        settings[setting_name.strip()] = value.strip()
    return settings