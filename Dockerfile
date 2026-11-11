FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble

ARG TARGETARCH

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl tar \
    && rm -rf /var/lib/apt/lists/*

RUN ARCH=$([ "$TARGETARCH" = "arm64" ] && echo "linux-arm64" || echo "linux-x64") \
    && curl -fsSL "https://github.com/vmikhailov/code-explorer/releases/latest/download/ce-${ARCH}.tar.gz" \
       | tar -xz -C /usr/local/bin \
    && chmod +x /usr/local/bin/ce

WORKDIR /workspace
ENTRYPOINT ["/usr/local/bin/ce", "mcp"]
