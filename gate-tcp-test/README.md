# gate-tcp-test

Waits for the RFID gate (board RFID7000) to connect over TCP and prints everything `card_7k.dll` reports.

1. **Build** (needs the .NET 8 SDK, and internet the first time): open a terminal in this folder and run `dotnet build`
2. **Run**: `bin\Debug\net8.0\win-x86\gate-tcp-test.exe` listens on all addresses, port 9090.
   To choose, add IP then port: `gate-tcp-test.exe 0.0.0.0 9090`
3. **Firewall**: when Windows asks about gate-tcp-test, tick **both** Private and Public, then click **Allow access**
   (Windows marks most networks Public). Clicked Cancel by mistake? Windows Security > Firewall & network protection > Allow an app through firewall.
4. **Gate**: set it to connect to one of the IP addresses the app prints, on the same port.

**What to expect:** after up to ~10 s, `open_tcp_server(...) returned hax_success (0)`. Then, for each gate:
`TCP OPEN` (the gate reached the laptop), then `CONNECT` (the gate answered the DLL, ~3 s later), then `TAGS` and `EPC` lines while tags are near.
`TCP OPEN` without `CONNECT` means the gate does not speak the DLL's protocol. No `TCP OPEN` at all means a network, IP, port or firewall problem.

**Keys:** `A` = `enable_alarm` on the last connected gate (acts on the real gate), `B0`/`B1` = try to set the beep off/on,
`L` = list connections, `Q` = quit (takes up to ~10 s). Every line is also saved to `test.log` next to the exe.

**About B0/B1:** the SDK's public config struct (`hax_7k_config_st`) has no beep field, so these keys read the config,
write it back unchanged, and re-read it, logging the full config before and after. They do **not** change the gate's
beep (that setting is not reachable through this API). `set_config` writes to the device, so only press B0/B1 with the
real gate connected and knowingly.
