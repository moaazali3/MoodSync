import sys
import json
import os
import warnings

# Suppress librosa/audioread and other user warnings to keep stderr clean
warnings.filterwarnings('ignore')

def analyze(file_path):
    if not os.path.exists(file_path):
        return {"error": f"File not found: {file_path}"}
        
    try:
        import librosa
        import numpy as np
    except ImportError as e:
        return {
            "error": f"Missing python dependencies. Please run: pip install librosa numpy soundfile (Detail: {str(e)})"
        }

    try:
        # Load only the first 30 seconds of audio to speed up processing
        y, sr = librosa.load(file_path, duration=30.0)
        
        # Calculate Tempo (BPM) using pure numpy autocorrelation to avoid numba JIT compilation crashes
        onset_env = librosa.onset.onset_strength(y=y, sr=sr)
        odf = onset_env - np.mean(onset_env)
        r = np.correlate(odf, odf, mode='full')
        r = r[len(r)//2:]
        fps = sr / 512.0
        min_lag = int(round(fps * 60.0 / 200.0))
        max_lag = int(round(fps * 60.0 / 50.0))
        
        if len(r) > max_lag:
            peak_lag = min_lag + np.argmax(r[min_lag:max_lag])
            tempo_val = float(60.0 * fps / peak_lag)
        else:
            tempo_val = 120.0
            
        # Calculate RMS Energy
        rms = librosa.feature.rms(y=y)
        energy = float(np.mean(rms))
        
        # Classification Rules based on acoustic features
        if energy >= 0.18 and tempo_val >= 100:
            mood = "Gaming_Mood"
        elif energy >= 0.12 and tempo_val >= 115:
            mood = "Happy_Vibes"
        elif energy < 0.06:
            mood = "SAD"
        else:
            mood = "Chill"
            
        # Confidence calculation based on proximity to mood centers
        if mood == "SAD":
            # Typical SAD: Energy ~ 0.02, BPM ~ 80
            dist_energy = max(0, energy - 0.02)
            dist_tempo = max(0, tempo_val - 80)
            confidence = max(0.5, 1.0 - (dist_energy * 15 + dist_tempo * 0.005))
        elif mood == "Gaming_Mood":
            # Typical Gaming_Mood: Energy ~ 0.22, BPM ~ 125
            dist_energy = max(0, 0.22 - energy)
            dist_tempo = max(0, 125 - tempo_val)
            confidence = max(0.5, 1.0 - (dist_energy * 5 + dist_tempo * 0.005))
        elif mood == "Happy_Vibes":
            # Typical Happy_Vibes: Energy ~ 0.15, BPM ~ 130
            dist_energy = abs(energy - 0.15)
            dist_tempo = max(0, 130 - tempo_val)
            confidence = max(0.5, 1.0 - (dist_energy * 5 + dist_tempo * 0.005))
        else: # Chill
            # Typical Chill: Energy ~ 0.09, BPM ~ 95
            dist_energy = abs(energy - 0.09)
            dist_tempo = abs(tempo_val - 95)
            confidence = max(0.5, 1.0 - (dist_energy * 8 + dist_tempo * 0.008))
            
        confidence = min(1.0, max(0.0, confidence))
            
        return {
            "mood": mood,
            "confidence": round(confidence, 2),
            "tempo": round(tempo_val, 2),
            "energy": round(energy, 4)
        }
    except Exception as ex:
        return {"error": f"Error during analysis: {str(ex)}"}

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(json.dumps({"error": "No file path provided"}))
        sys.exit(1)
        
    audio_path = sys.argv[1]
    result = analyze(audio_path)
    print(json.dumps(result))
