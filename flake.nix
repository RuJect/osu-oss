{
  description = "osu-cc - custom client for osu!lazer";

  inputs.nixpkgs.url = "nixpkgs";

  outputs = {
    self,
    nixpkgs,
  }: let
    systems = ["x86_64-linux"];
    forAllSystems = f: nixpkgs.lib.genAttrs systems (system: f (import nixpkgs {inherit system;}));
  in {
    formatter = forAllSystems (pkgs: pkgs.nixfmt);

    devShells = forAllSystems (pkgs: let
      sdk = pkgs.dotnet-sdk_10;
      tools = with pkgs; [ python314 uv bun libseccomp ];
      runtimeLibs = with pkgs; [
        libGL
        vulkan-loader
        wayland
        libxkbcommon
        libx11
        libxcursor
        libxext
        libxi
        libxrandr
        libxcb
        alsa-lib
        ffmpeg
      ];
    in {
      default = pkgs.mkShell {
        packages = [sdk] ++ tools ++ runtimeLibs ++ [
          (pkgs.writeShellScriptBin "run-osu" ''
            export LD_LIBRARY_PATH="${pkgs.lib.makeLibraryPath runtimeLibs}''${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
            dotnet run --project osu.Desktop -c Release
          '')
        ];

        env = {
          DOTNET_ROOT = "${sdk}/share/dotnet";
          DOTNET_NOLOGO = "1";
          DOTNET_CLI_TELEMETRY_OPTOUT = "1";
        };

        shellHook = ''
          export DOTNET_CLI_HOME="''${XDG_CACHE_HOME:-$HOME/.cache}/dotnet"
          export NUGET_PACKAGES="''${DOTNET_CLI_HOME}/NuGet/packages"
          # export LD_LIBRARY_PATH="${pkgs.lib.makeLibraryPath runtimeLibs}:$LD_LIBRARY_PATH"
        '';
      };
    });

    checks = forAllSystems (pkgs: let
      sdk = pkgs.dotnet-sdk_8;
    in {
      default = self.packages.${pkgs.system}.default;

      format = pkgs.stdenv.mkDerivation {
        name = "osucc-format-check";
        src = self;
        nativeBuildInputs = [sdk];
        buildPhase = ''
          dotnet restore osucc.sln --use-lock-file --locked-mode
          dotnet format osucc.sln --verify-no-changes --no-restore
        '';
        installPhase = "mkdir -p $out";
      };
    });
  };
}
