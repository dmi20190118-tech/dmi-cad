from PyInstaller.utils.hooks import collect_all, collect_delvewheel_libs_directory
datas, binaries, hiddenimports = collect_all("OCP")
more_datas, more_binaries = collect_delvewheel_libs_directory("OCP", libdir_name="cadquery_ocp.libs")
datas += more_datas
binaries += more_binaries
