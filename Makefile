ROOT_DIR := $(dir $(realpath $(lastword $(MAKEFILE_LIST))))

config ?= Debug
terrabuild ?= dotnet run --project $(ROOT_DIR)src/Terrabuild -c $(config) --
refresh ?= false
version ?= 0.0.0

current_dir = $(shell pwd)




#
#  _______   ___________    ____
# |       \ |   ____\   \  /   /
# |  .--.  ||  |__   \   \/   /
# |  |  |  ||   __|   \      /
# |  '--'  ||  |____   \    /
# |_______/ |_______|   \__/
#

build:
	dotnet build -c $(config) terrabuild.slnx

test:
	dotnet test terrabuild.slnx

parser:
	dotnet build -c $(config) /p:DefineConstants="GENERATE_PARSER"

clean:
	-rm terrabuild-debug.*
	-rm -rf $(PWD)/.out

upgrade:
	dotnet restore --force-evaluate

usage:
	$(terrabuild) --help
	$(terrabuild) run --help
	$(terrabuild) clear --help
	$(terrabuild) login --help

#
# .______       _______  __       _______     ___           _______. _______
# |   _  \     |   ____||  |     |   ____|   /   \         /       ||   ____|
# |  |_)  |    |  |__   |  |     |  |__     /  ^  \       |   (----`|  |__
# |      /     |   __|  |  |     |   __|   /  /_\  \       \   \    |   __|
# |  |\  \----.|  |____ |  `----.|  |____ /  _____  \  .----)   |   |  |____
# | _| `._____||_______||_______||_______/__/     \__\ |_______/    |_______|
#

publish:
	dotnet publish -c $(config) -p:Version=$(version) -o $(PWD)/.out/dotnet src/Terrabuild
	dotnet pack -c $(config) -p:Version=$(version) -o .out

publish-darwin:
	dotnet publish -c $(config) -r osx-x64 -p:PublishSingleFile=true --self-contained -p:Version=$(version) -p:IncludeNativeLibrariesForSelfExtract=true -o $(PWD)/.out/darwin/x64 src/Terrabuild
	dotnet publish -c $(config) -r osx-arm64 -p:PublishSingleFile=true --self-contained -p:Version=$(version) -p:IncludeNativeLibrariesForSelfExtract=true -o $(PWD)/.out/darwin/arm64 src/Terrabuild

publish-linux:
	dotnet publish -c $(config) -r linux-x64 -p:PublishSingleFile=true --self-contained -p:Version=$(version) -p:IncludeNativeLibrariesForSelfExtract=true -o $(PWD)/.out/linux/x64 src/Terrabuild
	dotnet publish -c $(config) -r linux-arm64 -p:PublishSingleFile=true --self-contained -p:Version=$(version) -p:IncludeNativeLibrariesForSelfExtract=true -o $(PWD)/.out/linux/arm64 src/Terrabuild

publish-windows:
	dotnet publish -c $(config) -r win-x64 -p:PublishSingleFile=true --self-contained -p:Version=$(version) -p:IncludeNativeLibrariesForSelfExtract=true -o $(PWD)/.out/windows/x64 src/Terrabuild
	dotnet publish -c $(config) -r win-arm64 -p:PublishSingleFile=true --self-contained -p:Version=$(version) -p:IncludeNativeLibrariesForSelfExtract=true -o $(PWD)/.out/windows/arm64 src/Terrabuild

publish-all: clean publish publish-darwin publish-linux publish-windows

docs:
	dotnet run --project tools/DocGen -- ../terrabuild.io/content/docs/extensions --write

try-docs:
	dotnet run --project tools/DocGen -- ../terrabuild.tagada

self: clean publish
	$(PWD)/.out/dotnet/terrabuild run build --configuration $(config) --retry --debug --log --local-only

self-ci: clean publish
	$(PWD)/.out/dotnet/terrabuild run build test dist --configuration $(config) --retry --debug --log --local-only

self-logs:
	$(PWD)/.out/dotnet/terrabuild logs build test dist --configuration $(config) --debug --log --local-only

logs:
	$(terrabuild) logs build test dist --configuration $(config) --debug --log --local-only

terrabuild:
	terrabuild run build test dist --configuration $(config) --retry --debug --log --local-only


#
# .___________. _______     _______.___________.    _______.
# |           ||   ____|   /       |           |   /       |
# `---|  |----`|  |__     |   (----`---|  |----`  |   (----`
#     |  |     |   __|     \   \       |  |        \   \
#     |  |     |  |____.----)   |      |  |    .----)   |
#     |__|     |_______|_______/       |__|    |_______/
#

test-scaffold:
	$(terrabuild) scaffold --workspace tests/scaffold --debug --log

test-rescaffold:
	$(terrabuild) scaffold --workspace tests/scaffold --debug --force --log

test-build-scaffold:
	$(terrabuild) run build --workspace tests/scaffold --debug --retry --log

test-build-simple:
	$(terrabuild) run build --workspace tests/simple --debug --retry --log --variable secret_message=tralala

test-rebuild-simple:
	$(terrabuild) run build --workspace tests/simple --debug --retry --log

test-deploy-simple:
	$(terrabuild) run deploy --workspace tests/simple --debug --retry --log

test-build-playground:
	$(terrabuild) run build --workspace ../playground --retry --debug

test-dist-playground:
	$(terrabuild) run dist --workspace ../playground --retry --debug

test-deploy-playground:
	$(terrabuild) run deploy --workspace ../playground --retry --debug

test-circular:
	$(terrabuild) run build --workspace tests/circular --debug --log

test-cluster-layers:
	$(terrabuild) run build -w tests/cluster-layers --debug --force

test-invalid-args:
	$(terrabuild) run build -w tests/cluster-layers --force --qewdiqoudhqioeudhqi

test-build-basic:
	$(terrabuild) run build -w tests/basic --force --debug --log


#      _______..___  ___.   ______    __  ___  _______    .___________. _______     _______.___________.    _______.
#     /       ||   \/   |  /  __  \  |  |/  / |   ____|   |           ||   ____|   /       |           |   /       |
#    |   (----`|  \  /  | |  |  |  | |  '  /  |  |__      `---|  |----`|  |__     |   (----`---|  |----`  |   (----`
#     \   \    |  |\/|  | |  |  |  | |    <   |   __|         |  |     |   __|     \   \       |  |        \   \
# .----)   |   |  |  |  | |  `--'  | |  .  \  |  |____        |  |     |  |____.----)   |      |  |    .----)   |
# |_______/    |__|  |__|  \______/  |__|\__\ |_______|       |__|     |_______|_______/       |__|    |_______/

define diff_file
	@if [ "$(refresh)" = "true" ]; then \
		cp $(1)/$(2) $(1)/results/$(2); \
	fi
	diff $(1)/results/$(2) $(1)/$(2)
endef

define diff_results
	$(call diff_file,$(1),terrabuild-debug.config.json)
	$(call diff_file,$(1),terrabuild-debug.node-graph.json)
	$(call diff_file,$(1),terrabuild-debug.action-graph.json)
	$(call diff_file,$(1),terrabuild-debug.cascade-graph.json)
	$(call diff_file,$(1),terrabuild-debug.cluster-graph.json)
	$(call diff_file,$(1),terrabuild-debug.info.md)
endef


define run_integration_test
	@printf "\n*** Running integration test %s ***\n" $(1)
	@$(terrabuild) version
	-cd $(1); rm terrabuild-debug.*
	cd $(1); GITHUB_SHA=1234 GITHUB_REF_NAME=main GITHUB_STEP_SUMMARY=terrabuild-debug.md GITHUB_REPOSITORY=magnusopera/terrabuild GITHUB_RUN_ID=42 $(terrabuild) $(2)
	$(call diff_results,$(1))
endef

# $(call run_integration_test, tests/cluster-layers, run build --force --debug -p 2 --log)

smoke-test-cluster-layers:
	$(call run_integration_test, tests/cluster-layers, run build --force --debug --parallel 2 --log --engine docker --local-only)

smoke-test-multirefs:
	$(call run_integration_test, tests/multirefs, run build --force --debug --parallel 2 --log --engine docker --local-only)

smoke-test-simple:
	$(call run_integration_test, tests/simple, run build test --force --debug --parallel 2 --log --engine docker --local-only)

smoke-indirect-target:
	$(call run_integration_test, tests/indirect-target, run build test plan apply --force --debug --parallel 1 --log --engine docker --local-only)

smoke-tests: smoke-test-cluster-layers smoke-test-multirefs smoke-test-simple smoke-indirect-target
