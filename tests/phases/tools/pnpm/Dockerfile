FROM alpine:3.20

RUN printf '#!/bin/sh\necho "pnpm toolchain image ready: $*"\n' > /usr/local/bin/pnpm \
    && chmod +x /usr/local/bin/pnpm
