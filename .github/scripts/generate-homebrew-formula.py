#!/usr/bin/env python3
"""
Generates Formula/ce.rb for Homebrew from release archives.
"""
import hashlib
import os
import sys

def sha256_of(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        while chunk := f.read(65536):
            h.update(chunk)
    return h.hexdigest()

def main():
    if len(sys.argv) < 4:
        print("Usage: generate-homebrew-formula.py <version> <dist_dir> <output_formula_path>", file=sys.stderr)
        sys.exit(1)

    version = sys.argv[1].lstrip("v").strip()
    dist_dir = sys.argv[2]
    out_file = sys.argv[3]

    sha_osx_arm64 = sha256_of(os.path.join(dist_dir, "ce-osx-arm64.tar.gz"))
    sha_osx_x64 = sha256_of(os.path.join(dist_dir, "ce-osx-x64.tar.gz"))
    sha_linux_arm64 = sha256_of(os.path.join(dist_dir, "ce-linux-arm64.tar.gz"))
    sha_linux_x64 = sha256_of(os.path.join(dist_dir, "ce-linux-x64.tar.gz"))

    base_url = f"https://github.com/vmikhailov/code-explorer/releases/download/v{version}"

    formula = f'''class Ce < Formula
  desc "Codebase intelligence CLI and knowledge graph engine"
  homepage "https://github.com/vmikhailov/code-explorer"
  version "{version}"
  license "MIT"

  on_macos do
    if Hardware::CPU.arm?
      url "{base_url}/ce-osx-arm64.tar.gz"
      sha256 "{sha_osx_arm64}"
    else
      url "{base_url}/ce-osx-x64.tar.gz"
      sha256 "{sha_osx_x64}"
    end
  end

  on_linux do
    if Hardware::CPU.arm?
      url "{base_url}/ce-linux-arm64.tar.gz"
      sha256 "{sha_linux_arm64}"
    else
      url "{base_url}/ce-linux-x64.tar.gz"
      sha256 "{sha_linux_x64}"
    end
  end

  def install
    bin.install "ce"
  end

  test do
    assert_match "ce", shell_output("#{{bin}}/ce --version", 2) rescue nil
  end
end
'''
    os.makedirs(os.path.dirname(os.path.abspath(out_file)), exist_ok=True)
    with open(out_file, "w", encoding="utf-8", newline="\n") as f:
        f.write(formula)
    print(f"Successfully generated {out_file} for v{version}")

if __name__ == "__main__":
    main()
