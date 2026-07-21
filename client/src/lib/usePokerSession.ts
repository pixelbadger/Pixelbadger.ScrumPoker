import { useCallback, useEffect, useRef, useState } from 'react';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
} from '@microsoft/signalr';
import { JoinResult, SessionView } from './types';
import { ownerKeyFor, participantKeyFor, saveParticipantKey } from './storage';

export type ConnectionStatus = 'connecting' | 'connected' | 'reconnecting' | 'error';

interface PokerSession {
  status: ConnectionStatus;
  state: SessionView | null;
  isOwner: boolean;
  myVote: string | null;
  castVote: (value: string) => void;
  reveal: () => void;
  reset: () => void;
}

export function usePokerSession(sessionId: string, name: string): PokerSession {
  const [status, setStatus] = useState<ConnectionStatus>('connecting');
  const [state, setState] = useState<SessionView | null>(null);
  const [isOwner, setIsOwner] = useState(false);
  const [myVote, setMyVote] = useState<string | null>(null);
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/poker')
      .withAutomaticReconnect()
      .build();
    connectionRef.current = connection;

    connection.on('state', (next: SessionView) => setState(next));

    const join = async () => {
      const result = await connection.invoke<JoinResult | null>(
        'Join',
        sessionId,
        name,
        participantKeyFor(sessionId),
        ownerKeyFor(sessionId),
      );
      if (!result) {
        setStatus('error');
        return;
      }
      saveParticipantKey(sessionId, result.participantKey);
      setIsOwner(result.isOwner);
      setMyVote(result.vote);
      setState(result.state);
      setStatus('connected');
    };

    connection.onreconnecting(() => setStatus('reconnecting'));
    connection.onreconnected(() => {
      // A new connection id means the server no longer knows us; join again.
      join().catch(() => setStatus('error'));
    });

    connection
      .start()
      .then(join)
      .catch(() => setStatus('error'));

    return () => {
      connection.stop();
    };
  }, [sessionId, name]);

  const invoke = useCallback((method: string, ...args: unknown[]) => {
    const connection = connectionRef.current;
    if (connection?.state === HubConnectionState.Connected) {
      connection.invoke(method, ...args).catch(() => undefined);
    }
  }, []);

  const castVote = useCallback(
    (value: string) => {
      setMyVote(value);
      invoke('CastVote', value);
    },
    [invoke],
  );

  const reveal = useCallback(() => invoke('Reveal'), [invoke]);

  const reset = useCallback(() => {
    setMyVote(null);
    invoke('Reset');
  }, [invoke]);

  // A new round clears everyone's vote server-side; mirror that locally.
  const round = state?.round;
  const prevRound = useRef(round);
  useEffect(() => {
    if (round !== undefined && prevRound.current !== undefined && round !== prevRound.current) {
      setMyVote(null);
    }
    prevRound.current = round;
  }, [round]);

  return { status, state, isOwner, myVote, castVote, reveal, reset };
}
