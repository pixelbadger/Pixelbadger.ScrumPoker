// Per-session identity lives in localStorage so refreshes and reconnects
// reattach to the same participant instead of creating a duplicate.

export const ownerKeyFor = (sessionId: string) =>
  localStorage.getItem(`sp:owner:${sessionId}`);

export const saveOwnerKey = (sessionId: string, key: string) =>
  localStorage.setItem(`sp:owner:${sessionId}`, key);

export const participantKeyFor = (sessionId: string) =>
  localStorage.getItem(`sp:participant:${sessionId}`);

export const saveParticipantKey = (sessionId: string, key: string) =>
  localStorage.setItem(`sp:participant:${sessionId}`, key);

export const savedName = () => localStorage.getItem('sp:name') ?? '';

export const saveName = (name: string) => localStorage.setItem('sp:name', name);
