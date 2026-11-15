"""Original temporary score for this project. No sampled recording or borrowed melody."""
from pathlib import Path
import wave, json
import numpy as np
RATE=22050; DURATION=64
ROOT=Path(__file__).resolve().parents[3]/'Assets/_Game/Chapters/Surveyor/Chapter/Audio'; ROOT.mkdir(exist_ok=True)

def frequency(midi): return 440*2**((midi-69)/12)
def note(track,at,midi,velocity):
    length=7; t=np.arange(length*RATE)/RATE; f=frequency(midi)
    signal=sum(weight*np.sin(2*np.pi*f*h*(1+.00007*h*h)*t)*np.exp(-t/(2.4/h**.4)) for h,weight in [(1,1),(2,.32),(3,.13),(4,.06),(5,.025)])
    signal*=np.minimum(t/.013,1)*velocity
    start=int(at*RATE)
    # Wrap the natural release across the loop; no truncated transient at the join.
    np.add.at(track,(np.arange(len(t))+start)%len(track),signal)

for name,roots,melody,pad_gain in [
    ('Departure',[52,48,55,50],[76,71,79,78,76,74,71,74],.028),
    ('Science',[48,55,52,50],[72,76,74,71,67,71,74,76],.018),
    ('Return',[48,52,55,50],[79,76,74,71,72,71,67,64],.034)]:
    track=np.zeros(DURATION*RATE,dtype=np.float64);time=np.arange(len(track))/RATE
    for bar in range(8):
        root=roots[bar%4];at=bar*8
        for offset,pitch,gain in [(0,root,.10),(1.8,root+7,.055),(3.8,root+12,.05),(6,melody[bar],.075)]: note(track,at+offset,pitch,gain)
    for pitch in [roots[0]-12,roots[0],roots[0]+7]:
        f=round(frequency(pitch)*DURATION)/DURATION
        track+=(np.sin(2*np.pi*f*time)+.2*np.sin(4*np.pi*f*time))*pad_gain*(.78+.22*np.sin(2*np.pi*time/DURATION))
    track=np.tanh(track);pcm=(np.clip(track,-.95,.95)*32767).astype('<i2')
    with wave.open(str(ROOT/(name+'.wav')),'wb') as out:out.setnchannels(1);out.setsampwidth(2);out.setframerate(RATE);out.writeframes(pcm.tobytes())
    print(name,float(np.sqrt(np.mean(track**2))),float(np.max(np.abs(track))))
(ROOT/'score.json').write_text(json.dumps({'authorship':'Original procedural temporary score, created in this project','sampleRate':RATE,'secondsEach':DURATION,'recordingsOrExternalComposition':False,'cues':['Departure','Science','Return']},indent=2),encoding='utf-8')
