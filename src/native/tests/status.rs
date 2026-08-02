#[test]
fn status_reports_process_alive_before_audio_host_is_running() {
    let output = std::process::Command::new(env!("CARGO_BIN_EXE_cleanchannel-audiohost"))
        .arg("--status")
        .output()
        .expect("AudioHost status command should start");

    assert!(output.status.success());

    let stdout = String::from_utf8_lossy(&output.stdout);
    assert!(stdout.contains("status: process-alive"));
    assert!(stdout.contains("ready=false"));
}
