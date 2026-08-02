use cleanchannel_audiohost::{HostConfig, HostMode, HostState};

fn main() {
    let arguments: Vec<_> = std::env::args_os().skip(1).collect();

    if arguments.len() == 1 && arguments[0] == "--status" {
        let config = HostConfig::default();
        let state = HostState::new();
        let mode = match config.mode() {
            HostMode::Lite => "Lite",
            HostMode::Full => "Full",
        };

        println!(
            "CleanChannel AudioHost status: {} (mode={mode}, lifecycle={:?}, ready={})",
            state.readiness_label(),
            state.lifecycle(),
            state.is_ready()
        );
        return;
    }

    eprintln!(
        "CleanChannel AudioHost: audio processing is not implemented yet; use --status for health."
    );
    std::process::exit(1);
}
