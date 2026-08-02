#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum HostMode {
    Lite,
    Full,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct HostConfig {
    mode: HostMode,
}

impl HostConfig {
    pub const fn new(mode: HostMode) -> Self {
        Self { mode }
    }

    pub const fn mode(self) -> HostMode {
        self.mode
    }
}

impl Default for HostConfig {
    fn default() -> Self {
        Self::new(HostMode::Lite)
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum HostLifecycle {
    Created,
    Starting,
    Running,
    Stopping,
    Stopped,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum HostError {
    InvalidTransition {
        from: HostLifecycle,
        to: HostLifecycle,
    },
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct HostState {
    lifecycle: HostLifecycle,
}

impl HostState {
    pub const fn new() -> Self {
        Self {
            lifecycle: HostLifecycle::Created,
        }
    }

    pub const fn lifecycle(self) -> HostLifecycle {
        self.lifecycle
    }

    pub const fn is_ready(self) -> bool {
        matches!(self.lifecycle, HostLifecycle::Running)
    }

    pub const fn readiness_label(self) -> &'static str {
        if self.is_ready() {
            "audio-host-ready"
        } else {
            "process-alive"
        }
    }

    pub fn transition(&mut self, next: HostLifecycle) -> Result<(), HostError> {
        let current = self.lifecycle;
        let valid = matches!(
            (current, next),
            (HostLifecycle::Created, HostLifecycle::Starting)
                | (HostLifecycle::Starting, HostLifecycle::Running)
                | (HostLifecycle::Running, HostLifecycle::Stopping)
                | (HostLifecycle::Stopping, HostLifecycle::Stopped)
                | (HostLifecycle::Stopped, HostLifecycle::Starting)
        );

        if valid {
            self.lifecycle = next;
            Ok(())
        } else {
            Err(HostError::InvalidTransition {
                from: current,
                to: next,
            })
        }
    }
}

#[cfg(test)]
mod tests {
    use super::{HostConfig, HostError, HostLifecycle, HostMode, HostState};

    #[test]
    fn accepts_valid_lifecycle_transitions() {
        let mut state = HostState::new();

        state.transition(HostLifecycle::Starting).unwrap();
        state.transition(HostLifecycle::Running).unwrap();
        state.transition(HostLifecycle::Stopping).unwrap();
        state.transition(HostLifecycle::Stopped).unwrap();
        state.transition(HostLifecycle::Starting).unwrap();

        assert_eq!(state.lifecycle(), HostLifecycle::Starting);
    }

    #[test]
    fn rejects_invalid_lifecycle_transitions_without_mutating_state() {
        let mut state = HostState::new();

        let error = state.transition(HostLifecycle::Running).unwrap_err();

        assert_eq!(
            error,
            HostError::InvalidTransition {
                from: HostLifecycle::Created,
                to: HostLifecycle::Running,
            }
        );
        assert_eq!(state.lifecycle(), HostLifecycle::Created);
    }

    #[test]
    fn configuration_supports_lite_and_full_modes() {
        assert_eq!(HostConfig::new(HostMode::Lite).mode(), HostMode::Lite);
        assert_eq!(HostConfig::new(HostMode::Full).mode(), HostMode::Full);
        assert_eq!(HostConfig::default().mode(), HostMode::Lite);
    }

    #[test]
    fn created_host_is_alive_but_not_audio_ready() {
        let mut state = HostState::new();

        assert!(!state.is_ready());
        assert_eq!(state.readiness_label(), "process-alive");

        state.transition(HostLifecycle::Starting).unwrap();
        state.transition(HostLifecycle::Running).unwrap();

        assert!(state.is_ready());
        assert_eq!(state.readiness_label(), "audio-host-ready");
    }
}
