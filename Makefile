PREFIX        ?= /usr/local
SBINDIR       ?= $(PREFIX)/sbin
SYSCONFDIR    ?= $(PREFIX)/etc
LOCALSTATEDIR ?= /var
CONFIGURATION ?= Release
PUBLISH_ARGS  ?= -p:DebugSymbols=false -p:DebugType=None \
                 -p:InvariantGlobalization=true \
                 -p:PublishSingleFile=true \
                 -p:PublishTrimmed=true -p:SuppressTrimAnalysisWarnings=true
RID           ?= linux-x64
SERVICE_USER  ?= nobody

PROJECT          := $(shell perl -ne '/"([^"]+\.csproj)"/ && print $$1 =~ s:\\:/:r and last' *.sln)
PROJECT_DIR      := $(shell dirname $(PROJECT))
PROJECT_NAME     := $(shell basename $(PROJECT) .csproj)
TARGET_FRAMEWORK := $(shell perl -ne '/<(TargetFramework)>(.*)<\/\1>/ && print $$2' $(PROJECT))
ASSEMBLY_NAME    := $(shell perl -ne '/<(AssemblyName)>(.*)<\/\1>/ && print $$2' $(PROJECT))
ifeq ($(ASSEMBLY_NAME),)
	ASSEMBLY_NAME = $(PROJECT_NAME)
endif

SRCS   := $(shell find $(PROJECT_DIR) -name *.cs) $(PROJECT)
OUTDIR := $(PROJECT_DIR)/bin/$(CONFIGURATION)/$(TARGET_FRAMEWORK)/$(RID)/publish

.PHONY: all docs install clean

all: $(OUTDIR)/$(ASSEMBLY_NAME) $(OUTDIR)/$(ASSEMBLY_NAME).service

docs:
	mkdocs build
	install -m 644 src/wwwroot/favicon.ico src/wwwroot/favicon.png site/
	perl -pi -e 's|img/favicon.ico|favicon.png" media="(prefers-color-scheme: dark)|g' site/*.html site/*/*.html
	$(RM) site/img/favicon.ico

install: all
	install -d -m 755 $(SYSCONFDIR)/$(ASSEMBLY_NAME)/wwwroot
	install -d -m 700 -o $(SERVICE_USER) $(LOCALSTATEDIR)/lib/$(ASSEMBLY_NAME)
	install -m 644 $(OUTDIR)/wwwroot/* $(SYSCONFDIR)/$(ASSEMBLY_NAME)/wwwroot
	install -m 644 $(OUTDIR)/appsettings.json $(SYSCONFDIR)/$(ASSEMBLY_NAME)/appsettings.json
	install -m 755 $(OUTDIR)/$(ASSEMBLY_NAME) $(SBINDIR)/$(ASSEMBLY_NAME)
	install -m 644 $(OUTDIR)/$(ASSEMBLY_NAME).service /etc/systemd/system/$(ASSEMBLY_NAME).service

clean:
	$(RM) -r $(PROJECT_DIR)/bin $(PROJECT_DIR)/obj site

$(OUTDIR)/$(ASSEMBLY_NAME): $(SRCS)
	dotnet publish --nologo -c $(CONFIGURATION) $(PUBLISH_ARGS) -r $(RID) --sc $(PROJECT)

$(OUTDIR)/$(ASSEMBLY_NAME).service: $(ASSEMBLY_NAME).service.in
	mkdir -p $(OUTDIR)
	cat $(ASSEMBLY_NAME).service.in \
	  | sed -e 's:@ASSEMBLY_NAME@:$(ASSEMBLY_NAME):' \
	        -e 's:@SBINDIR@:$(SBINDIR):' \
	        -e 's:@SYSCONFDIR@:$(SYSCONFDIR):' \
	        -e 's:@LOCALSTATEDIR@:$(LOCALSTATEDIR):' \
	        -e 's:@SERVICE_USER@:$(SERVICE_USER):' \
	  > $@
