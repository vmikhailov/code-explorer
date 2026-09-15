class Ce < Formula
  desc "Ultra-fast code graph exploration & architecture mapping CLI"
  homepage "https://github.com/vmikhailov/code-explorer"
  version "1.0.0"

  on_macos do
    if Hardware::CPU.arm?
      url "https://github.com/vmikhailov/code-explorer/releases/download/v#{version}/ce-osx-arm64.tar.gz"
      # sha256 "<insert sha256 here>"
    else
      url "https://github.com/vmikhailov/code-explorer/releases/download/v#{version}/ce-osx-x64.tar.gz"
      # sha256 "<insert sha256 here>"
    end
  end

  on_linux do
    if Hardware::CPU.arm?
      url "https://github.com/vmikhailov/code-explorer/releases/download/v#{version}/ce-linux-arm64.tar.gz"
      # sha256 "<insert sha256 here>"
    else
      url "https://github.com/vmikhailov/code-explorer/releases/download/v#{version}/ce-linux-x64.tar.gz"
      # sha256 "<insert sha256 here>"
    end
  end

  def install
    bin.install "ce"
  end

  test do
    assert_match "ce", shell_output("#{bin}/ce --version")
  end
end
