/** What a network account is used for. */
export type AccountType = 'personal' | 'service' | 'managed';

/** Account lifecycle. */
export type AccountStatus = 'active' | 'locked' | 'disabled';

/** Kind of legal entity. */
export type LegalEntityType = 'naturalPerson' | 'legalPerson' | 'government' | 'other';

/** Kind of identity document. */
export type IdDocumentType =
  | 'residentIdentityCard'
  | 'passport'
  | 'unifiedSocialCreditCode'
  | 'other';

/** Real-name verification lifecycle. */
export type VerificationState = 'pending' | 'verified' | 'rejected' | 'expired';

/** A login identity returned by `/api/v1/accounts`. */
export interface Account {
  id: string;
  legalEntityId: string | null;
  realm: string;
  username: string;
  email: string | null;
  phoneNumber: string | null;
  displayName: string | null;
  type: AccountType;
  status: AccountStatus;
  isPrimary: boolean;
  roles: string[];
  createdAt: string;
  updatedAt: string;
  disabledAt: string | null;
}

/** The non-secret part of a legal entity's real-name identity. */
export interface RealName {
  documentType: IdDocumentType;
  country: string;
}

/** A verified subject returned by `/api/v1/legal-entities`. */
export interface LegalEntity {
  id: string;
  type: LegalEntityType;
  displayName: string;
  realName: RealName | null;
  verificationState: VerificationState;
  verifiedAt: string | null;
  providerReference: string | null;
  createdAt: string;
  updatedAt: string;
}

/** A role returned by `/api/v1/roles`. */
export interface Role {
  id: string;
  name: string;
  description: string | null;
  permissions: string[];
  createdAt: string;
  updatedAt: string;
}

/** One cursor page. The cursor is opaque; never derive offsets from it. */
export interface Page<T> {
  items: T[];
  nextCursor: string | null;
  hasMore: boolean;
}
