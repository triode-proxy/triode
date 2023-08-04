%define debug_package %{nil}
%define __strip /bin/true

%ifarch aarch64
%define rid linux-arm64
%else
%define rid linux-x64
%endif

Name:              triode
Version:           0.1.0
Release:           0%{?dist}
Summary:           DNS-based selective HTTP debugging proxy
License:           AGPLv3
URL:               https://triode-proxy.github.io
Source0:           https://github.com/triode-proxy/triode/archive/refs/tags/v0.1.0.tar.gz#/triode-%{version}.tar.gz

BuildRequires:     dotnet-sdk-6.0
BuildRequires:     make
BuildRequires:     perl

Provides:          webserver
Requires:          libicu
Requires(post):    systemd-units
Requires(preun):   systemd-units
Requires(postun):  systemd-units

%description
%{summary}.

%package tools
Summary:           Tools for Triode service
BuildArch:         noarch
Requires:          triode
Requires:          openssl

%description tools
%{summary}.

%prep
%setup -q

%build

make RID=%{rid} \
     SBINDIR=%{_sbindir} \
     SYSCONFDIR=%{_sysconfdir} \
     LOCALSTATEDIR=%{_localstatedir} \
     SERVICE_USER=triode

sed -i.bak 's:#!.*:#!/usr/libexec/platform-python:' tools/triode-certs
sed -i.bak 's:#!.*:#!/usr/libexec/platform-python:' tools/triode-trace

%install
%define outdir src/bin/Release/net6.0/%{rid}/publish
mkdir -p %{buildroot}%{_sbindir} \
         %{buildroot}%{_sysconfdir}/triode/wwwroot \
         %{buildroot}%{_localstatedir}/lib/triode \
         %{buildroot}%{_unitdir}
install -m 755 %{outdir}/triode           %{buildroot}%{_sbindir}
install -m 644 %{outdir}/appsettings.json %{buildroot}%{_sysconfdir}/triode
install -m 644 %{outdir}/wwwroot/*        %{buildroot}%{_sysconfdir}/triode/wwwroot
install -m 644 %{outdir}/triode.service   %{buildroot}%{_unitdir}
install -m 755 tools/triode-certs         %{buildroot}%{_sbindir}
install -m 755 tools/triode-trace         %{buildroot}%{_sbindir}

%pre
getent group triode > /dev/null || groupadd -r triode
getent passwd triode > /dev/null || useradd -c "Triode" \
                                            -d %{_localstatedir}/lib/triode \
                                            -g triode -s /sbin/nologin \
                                            -r triode
exit 0

%post
%systemd_post triode.service

%preun
%systemd_preun triode.service

%postun
%systemd_postun triode.service

%files
%defattr(-,root,root,-)
%license LICENSE
%doc README.md
%{_sbindir}/triode
%{_sysconfdir}/triode/appsettings.json
%{_sysconfdir}/triode/wwwroot/*
%{_unitdir}/triode.service
%attr(0700,triode,triode) %dir %{_localstatedir}/lib/triode

%files tools
%defattr(-,root,root,-)
%{_sbindir}/triode-certs
%{_sbindir}/triode-trace

%changelog
* Sat Aug  5 2023 MALU <contact@andantissimo.jp> - 0.1.0-0
- substitute both request and response bodies
- simplify DNS TTLs
- and some fixes

* Sat Jul 22 2023 MALU <contact@andantissimo.jp> - 0.0.0-0
- initial release
