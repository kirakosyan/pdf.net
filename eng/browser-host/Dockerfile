FROM debian:trixie-slim@sha256:62bd423aeee912639ca8ee681c2b762cb3e25137e0a92b20be455898a9f034ad
# This explicit source-fixture build performs provisioning. Runtime jobs never install or download assets.
RUN rm /etc/apt/sources.list.d/debian.sources && \
    printf 'deb [check-valid-until=no] http://snapshot.debian.org/archive/debian/20261020T000000Z trixie main\ndeb [check-valid-until=no] http://snapshot.debian.org/archive/debian-security/20261020T000000Z trixie-security main\n' > /etc/apt/sources.list && \
    apt-get -o Acquire::Check-Valid-Until=false update && \
    apt-get install -y --no-install-recommends chromium python3 ca-certificates && \
    rm -rf /var/lib/apt/lists/* && \
    mkdir -p /opt/fluent-pdf/browser /opt/fluent-pdf/fonts && \
    dpkg-query -W -f='${binary:Package}\t${Version}\t${Architecture}\n' > /opt/fluent-pdf/browser/packages.tsv
COPY host.py fontconfig.xml /opt/fluent-pdf/browser/
COPY business.ttf /opt/fluent-pdf/fonts/business.ttf
RUN /usr/bin/chromium --version > /opt/fluent-pdf/browser/browser-version.txt && \
    find /usr/lib/chromium /opt/fluent-pdf -type f ! -name files.sha256 -exec sha256sum '{}' ';' | sort > /opt/fluent-pdf/browser/files.sha256
ENV HOME=/tmp/browser-home LANG=C.UTF-8 FONTCONFIG_FILE=/opt/fluent-pdf/browser/fontconfig.xml
USER 65532:65532
WORKDIR /tmp
ENTRYPOINT ["/usr/bin/python3", "-I", "/opt/fluent-pdf/browser/host.py"]
