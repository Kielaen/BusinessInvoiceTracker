import { describe, expect, it } from 'vitest'
import {
  zCurrentUserResponse,
  zWalkingSkeletonAccepted,
  zWalkingSkeletonStatus,
} from './generated/zod.gen'

describe('generated API contracts', () => {
  it('validates an authenticated tenant membership', () => {
    const currentUser = zCurrentUserResponse.parse({
      userId: '4d356c2d-08e5-4cbf-bb83-8bd7f1254661',
      email: 'owner@acme.test',
      memberships: [
        {
          businessId: '11111111-1111-4111-8111-111111111111',
          businessName: 'Acme Test Business',
          role: 'Owner',
        },
      ],
    })

    expect(currentUser.memberships[0]?.role).toBe('Owner')
  })

  it('accepts the documented API shapes', () => {
    expect(
      zWalkingSkeletonAccepted.parse({
        probeId: 'b8be1d65-23f9-4b42-8c63-9056cf57f7f9',
        messageId: 'c57f89c1-0a79-4011-b805-4a71e70c2409',
        status: 'pending',
      }).status,
    ).toBe('pending')

    expect(
      zWalkingSkeletonStatus.parse({
        probeId: 'b8be1d65-23f9-4b42-8c63-9056cf57f7f9',
        status: 'processed',
        processedAtUtc: '2026-07-31T08:03:42.337311Z',
      }).status,
    ).toBe('processed')
  })

  it('rejects a malformed identifier at runtime', () => {
    expect(() =>
      zWalkingSkeletonAccepted.parse({
        probeId: 'not-a-uuid',
        messageId: 'also-not-a-uuid',
        status: 'pending',
      }),
    ).toThrow()
  })
})
