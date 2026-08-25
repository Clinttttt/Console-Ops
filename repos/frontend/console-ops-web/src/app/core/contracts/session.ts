/**
 * The operator Console Ops is acting as.
 *
 * The identity is GitHub's, not one Console Ops invented: the same account that authorized the App is the actor the
 * provider records against a run, so the screen and the run history name the same person.
 */
export interface OperatorSession {
  readonly login: string;
  /** `null` when GitHub reports no avatar, which is a fact rather than a missing image. */
  readonly avatarUrl: string | null;
  /** ISO-8601 UTC. */
  readonly signedInAt: string;
  /** When the GitHub token stops working. The API renews it while the session is being used. */
  readonly expiresAt: string;
}

/**
 * Why a sign-in attempt came back without a session.
 *
 * Reported as a code rather than a message because the reason arrives in a redirect parameter, and wording chosen
 * by the screen is safer to show than text reflected from a URL.
 */
export type SignInRefusal =
  | 'state'
  | 'declined'
  | 'Auth.NotAnOperator'
  | 'Auth.NoOperatorsConfigured'
  | 'Auth.CodeRejected'
  | 'Auth.ProviderUnavailable'
  | 'unknown';
