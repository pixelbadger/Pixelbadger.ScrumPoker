export interface ParticipantView {
  name: string;
  isOwner: boolean;
  hasVoted: boolean;
  connected: boolean;
  vote: string | null;
}

export interface SessionView {
  id: string;
  name: string;
  round: number;
  revealed: boolean;
  allVoted: boolean;
  participants: ParticipantView[];
  deck: string[];
}

export interface JoinResult {
  participantKey: string;
  isOwner: boolean;
  vote: string | null;
  state: SessionView;
}
