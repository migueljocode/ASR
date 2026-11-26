import asyncio
import os
import subprocess
import edge_tts

VOICES = {
    "persian": (
        "سلام، امروز هوا خیلی خوب است. من در حال آزمایش سیستم تشخیص گفتار هستم.",
        "fa-IR-DilaraNeural",
    ),
    "english": (
        "Hello, the weather is very nice today. I am testing my speech recognition system.",
        "en-US-AriaNeural",
    ),
}


async def generate_tts(output_dir: str) -> None:
    for name, (text, voice) in VOICES.items():
        mp3_path = os.path.join(output_dir, f"{name}.mp3")
        communicate = edge_tts.Communicate(text=text, voice=voice)
        await communicate.save(mp3_path)


def main() -> None:
    output_dir = "asr_samples"
    os.makedirs(output_dir, exist_ok=True)

    # 1. Generate Persian and English MP3 files via edge-tts
    asyncio.run(generate_tts(output_dir))

    # 2. Convert MP3 files to 16kHz mono WAV
    for name in VOICES:
        mp3_path = os.path.join(output_dir, f"{name}.mp3")
        wav_path = os.path.join(output_dir, f"{name}.wav")
        subprocess.run(
            ["ffmpeg", "-y", "-i", mp3_path, "-ar", "16000", "-ac", "1", wav_path],
            check=True,
        )

    # 3. Generate 0.5s silence and concatenate Persian + silence + English
    silence_path = os.path.join(output_dir, "silence.wav")
    concat_list_path = os.path.join(output_dir, "concat.txt")
    combined_wav = os.path.join(output_dir, "combined.wav")
    combined_mp3 = os.path.join(output_dir, "combined.mp3")

    subprocess.run(
        [
            "ffmpeg",
            "-y",
            "-f",
            "lavfi",
            "-i",
            "anullsrc=r=16000:cl=mono",
            "-t",
            "0.5",
            silence_path,
        ],
        check=True,
    )

    with open(concat_list_path, "w", encoding="utf-8") as f:
        f.write("file 'persian.wav'\n")
        f.write("file 'silence.wav'\n")
        f.write("file 'english.wav'\n")

    subprocess.run(
        [
            "ffmpeg",
            "-y",
            "-f",
            "concat",
            "-safe",
            "0",
            "-i",
            concat_list_path,
            "-c",
            "copy",
            combined_wav,
        ],
        check=True,
    )

    subprocess.run(
        [
            "ffmpeg",
            "-y",
            "-i",
            combined_wav,
            "-codec:a",
            "libmp3lame",
            "-q:a",
            "2",
            combined_mp3,
        ],
        check=True,
    )

    print("Generated 6 audio files in:", os.path.abspath(output_dir))


if __name__ == "__main__":
    main()
