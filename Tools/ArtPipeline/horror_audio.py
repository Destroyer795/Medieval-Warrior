"""Deterministic, sample-free foley and aperiodic undead voices (mono, 44.1kHz)."""
import hashlib
import numpy as np
from scipy import signal

SR = 44100


class Foley:
    def __init__(self, name, duration):
        self.rng = np.random.default_rng(int.from_bytes(hashlib.sha256(name.encode()).digest()[:8], 'little'))
        self.n = int(SR * duration)
        self.t = np.arange(self.n) / SR
        self.d = duration

    def band(self, x, low, high):
        return signal.sosfilt(signal.butter(2, [low, high], btype='bandpass', fs=SR, output='sos'), x)

    def noise(self, low=80, high=12000):
        return self.band(self.rng.normal(0, 1, self.n), low, high)

    def curve(self, rate=8):
        count = max(4, int(self.d * rate) + 2)
        return np.interp(self.t, np.linspace(0, self.d, count), self.rng.uniform(-1, 1, count))

    def decay(self, rate, delay=0):
        return (self.t >= delay) * np.exp(-np.maximum(0, self.t-delay)*rate)

    def tone(self, start, end, decay, delay=0):
        f = np.linspace(start, end, self.n)
        return np.sin(2*np.pi*np.cumsum(f)/SR) * self.decay(decay, delay)

    def grains(self, rate, low, high, decay=90, accelerate=False):
        events = self.rng.uniform(0, 1, max(1, int(self.d*rate)))
        events = np.sqrt(events) if accelerate else events
        envelope = np.zeros(self.n)
        for when in events*self.d:
            envelope += self.decay(decay*self.rng.uniform(.6, 1.8), when)*self.rng.uniform(.2, 1)
        return self.noise(low, high)*envelope

    def resonance(self, source, center, bandwidth):
        # Continuous time-varying vocal-tract resonance. Filter memory survives
        # every sample; no independently filtered chunks or periodic reset clicks.
        omega=2*np.pi*center/SR;alpha=np.sin(omega)*bandwidth/(2*center)
        b=alpha/(1+alpha);a1=-2*np.cos(omega)/(1+alpha);a2=(1-alpha)/(1+alpha)
        out=np.zeros(self.n)
        for i in range(2,self.n):
            out[i]=b[i]*(source[i]-source[i-2])-a1[i]*out[i-1]-a2[i]*out[i-2]
        return out/(np.sqrt(np.mean(out*out))+1e-9)

    def throat(self, start=78, end=43):
        # Missing/irregular glottal closures excite NOISE-rich resonances. There
        # is no sawtooth carrier: unstable vocal fry, breath and saliva dominate.
        frequency = np.linspace(start, end, self.n)*np.exp(.16*self.curve(17)+.07*self.curve(43))
        phase = np.cumsum(frequency)/SR
        cycle = phase.astype(int)
        closures = self.rng.uniform(.1, 1, cycle[-1]+2)
        closures[self.rng.random(len(closures)) < .19] = 0
        opening=phase%1
        flow=np.where(opening<.58,np.sin(np.pi*np.minimum(opening/.58,1))**2,0)*closures[cycle]
        pulse=np.diff(flow,prepend=0)*SR/np.maximum(frequency,20)
        source=self.band(pulse,45,6500)*.6+self.noise(90,6200)*.25
        rattle=self.resonance(source,185+35*self.curve(7),95)*.7
        rattle+=self.resonance(source,610+130*self.curve(5),190)*.55
        rattle+=self.resonance(source,1550+250*self.curve(9),350)*.22
        choking = np.clip(.62+.45*self.curve(6)+.22*self.curve(25), .08, 1)
        sub = self.tone(min(85, start*.8), max(40, end), 1.1)*(.45+.3*self.curve(13))
        breath = self.noise(1200, 8500)*(.1+.26*np.maximum(0, self.curve(9)))
        saliva = self.grains(19, 240, 3800, 170)*.19
        envelope = np.minimum(1, self.t/.045)*np.minimum(1, (self.d-self.t)/(.4*self.d))**1.5
        return (np.tanh(rattle*.85)*choking+sub*.45+breath+saliva)*envelope

    def finish(self, x):
        x = self.band(x, 28, 15500)
        # Rounded transients, no DC, and click-free boundaries; leave digital headroom.
        x = np.tanh(x*1.2)
        fade = min(220, len(x)//3)
        x[:fade] *= np.linspace(0, 1, fade)
        x[-fade:] *= np.linspace(1, 0, fade)
        assert np.isfinite(x).all()
        return x/(np.max(np.abs(x)) or 1)*.9


def build_bank():
    bank = {}
    for i, duration in enumerate((1.55, 1.85, 2.1)):
        name = f'zombie_groan{i}'; f = Foley(name, duration)
        bank[name] = f.finish(f.throat(76-i*9, 46+i*3))

    f = Foley('zombie_attack', .66)
    intake = f.noise(3200, 13000)*np.exp(-((f.t-.075)/.045)**2)
    jaw = f.noise(1000, 11000)*(f.decay(240,.105)+.35*f.decay(300,.125))
    bite = f.throat(155, 58)*np.minimum(1, f.t/.1)
    bank['zombie_attack'] = f.finish(intake*.6+jaw+bite*1.5)

    f = Foley('zombie_die', 1.65)
    lung = f.noise(150, 6000)*f.decay(2.5)*(.4+.6*np.maximum(0, f.curve(18)))
    crunch = f.grains(17, 650, 7000, 150)*f.decay(8)
    bank['zombie_die'] = f.finish(f.throat(91, 38)*.7+lung+crunch*.5+f.tone(62, 29, 2.4)*.5)

    f = Foley('brute_roar', 2.05)
    ph = 2*np.pi*np.cumsum(np.linspace(66, 37, f.n)*(1+.15*f.curve(17)))/SR
    chest = sum(signal.sawtooth(ph*octave)*weight for octave, weight in ((.5,.5),(1,.4),(2,.14)))
    roar = f.throat(78, 42)+f.band(np.tanh(chest*3), 45, 1500)*.6
    for delay, gain in ((.047,.22),(.083,.15),(.137,.09)):
        offset = int(delay*SR); roar[offset:] += roar[:-offset].copy()*gain
    bank['brute_roar'] = f.finish(roar*np.minimum(1, f.t/.04)*np.minimum(1,(f.d-f.t)/.65))

    f = Foley('spit', .65)
    bank['spit'] = f.finish(f.throat(112, 54)*.55+f.grains(38, 120, 3000, 65)*np.exp(-((f.t-.25)/.22)**2))
    f = Foley('splat', .85)
    bank['splat'] = f.finish(f.noise(90, 2300)*f.decay(15)*2+f.grains(46, 140, 4200, 90)*f.decay(5)
                            +f.noise(4000, 13000)*f.decay(4, .06)*.32+f.tone(180, 42, 17)*.5)
    f = Foley('bloater_fuse', 1.35)
    strain = f.throat(63, 128)*(.3+f.t/f.d)
    bank['bloater_fuse'] = f.finish(strain+f.grains(48, 100, 2800, 75, True)*(.2+f.t/f.d))
    f = Foley('explosion', 1.8)
    bank['explosion'] = f.finish(f.noise(35, 480)*f.decay(4)*2.3+f.tone(95, 29, 4)*1.1
                                +f.noise(1500, 12000)*f.decay(24)*.65+f.grains(35, 500, 8500, 130)*f.decay(3, .08)*.5)
    f = Foley('emerge', 1.2)
    bank['emerge'] = f.finish(f.grains(40, 90, 2700, 45)*np.sin(np.pi*f.t/f.d)**.6
                             +f.grains(15, 1700, 9000, 160)*.35+f.noise(35, 140)*np.sin(np.pi*f.t/f.d)*.65)

    f = Foley('sword_swing', .29)
    swish = np.exp(-((f.t-.105)/.052)**2)
    bank['sword_swing'] = f.finish(f.noise(550, 12000)*swish+f.noise(110, 950)*swish*.65)
    f = Foley('sword_hit', .36)
    cut = f.noise(1800, 12000)*f.decay(95)
    flesh = f.noise(65, 950)*f.decay(22)*1.4+f.tone(145, 48, 22)*.6
    bone = f.noise(900, 6500)*(f.decay(160,.018)+f.decay(210,.039))*.38
    bank['sword_hit'] = f.finish(cut+flesh+bone)
    f = Foley('shield_block', .68)
    iron = sum(np.sin(2*np.pi*freq*f.t)*f.decay(decay)*weight for freq, decay, weight in
               ((379,15,.4),(811,19,.3),(1372,24,.2),(2437,34,.1)))
    bank['shield_block'] = f.finish(iron+f.noise(500, 11000)*f.decay(95)+f.noise(70, 1250)*f.decay(25)*1.2+f.tone(158,67,22)*.5)
    for name, wet in (('arrow_hit',False),('arrow_flesh',True)):
        f = Foley(name, .33)
        flight = f.noise(3800, 14000)*np.exp(-((f.t-.012)/.009)**2)*.4
        impact = f.noise(80, 1400)*f.decay(32,.025)+f.tone(185,58,28,.025)*.6
        texture = f.noise(600 if wet else 1800, 5600 if wet else 11000)*f.decay(80,.026)*.6
        bank[name] = f.finish(flight+impact+texture)
    f = Foley('dodge', .44)
    leather = f.grains(32, 150, 2600, 50)*np.sin(np.pi*f.t/f.d)
    mail = f.grains(24, 2300, 11000, 180)*.24
    bank['dodge'] = f.finish(leather+mail+f.noise(100,2200)*f.decay(16,.16)*.45)
    f = Foley('player_hurt', .38)
    bank['player_hurt'] = f.finish(f.throat(126, 77)+f.noise(130,2200)*f.decay(18)*.5)

    # Bowstring snap, two damped limb modes and the shaft clearing the rest.
    # No pitched Karplus–Strong musical pluck or half-second synthetic whistle.
    f=Foley('bow_release',.24)
    string=f.noise(1800,14000)*f.decay(260)
    limbs=sum(np.sin(2*np.pi*frequency*f.t)*f.decay(decay)*weight for frequency,decay,weight in
              ((142,48,.2),(317,68,.1),(617,100,.045)))
    shaft=f.noise(1700,10500)*np.exp(-((f.t-.034)/.017)**2)*.42
    bank['bow_release']=f.finish(string+limbs+shaft+f.noise(180,1500)*f.decay(95)*.32)

    f = Foley('parry', .52)
    ring = sum(np.sin(2*np.pi*freq*f.t)*f.decay(28+i*7)*.2 for i,freq in enumerate((630,1387,2611,4177)))
    bank['parry'] = f.finish(ring+f.noise(1400,14000)*f.decay(85)+f.tone(180,67,26)*.7)
    f = Foley('thunder_guard', .85)
    bank['thunder_guard'] = f.finish(f.noise(35, 400)*f.decay(6)+f.grains(35,1300,13000,220)*f.decay(9)*.7+f.tone(78,35,7)*.8)
    f = Foley('phantom_cleave', .52)
    bank['phantom_cleave'] = f.finish(f.noise(120,8500)*np.exp(-((f.t-.13)/.085)**2)+f.throat(48,41)*.15)
    f = Foley('splinter', .42)
    bank['splinter'] = f.finish(f.grains(35,400,10000,140)*f.decay(11)+f.noise(100,850)*f.decay(33))
    return bank
