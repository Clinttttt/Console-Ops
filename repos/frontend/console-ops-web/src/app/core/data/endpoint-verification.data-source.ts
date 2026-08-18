import { Observable } from 'rxjs';

import {
  EndpointVerification,
  EndpointVerificationRequest,
} from '../contracts/endpoint-verification';

/**
 * Port for probing candidate endpoints before registration.
 *
 * Verification is optional: the screen must remain usable when it is unavailable, and a failed probe
 * must never block registration.
 */
export abstract class EndpointVerificationDataSource {
  abstract verify(request: EndpointVerificationRequest): Observable<EndpointVerification>;
}
