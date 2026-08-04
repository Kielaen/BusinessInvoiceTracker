import { describe, expect, it } from 'vitest';
import {
  zGetInvoiceContractResponse,
  zProblemDetails,
} from './generated/zod.gen';

const validInvoice = {
  id: '11111111-1111-4111-8111-111111111111',
  status: 'Issued',
  total: '1234.50',
  issuedAt: '2026-07-30T12:00:00Z',
  purchaseOrder: null,
  page: { page: 1, pageSize: 25, totalCount: 1 },
};

describe('generated runtime contract', () => {
  it('accepts the representative ASP.NET response', () => {
    expect(zGetInvoiceContractResponse.parse(validInvoice)).toEqual(validInvoice);
  });

  it.each([
    ['decimal encoded as a JSON number', { ...validInvoice, total: 1234.5 }],
    ['decimal with the wrong scale', { ...validInvoice, total: '1234.5' }],
    ['unknown enum value', { ...validInvoice, status: 'Unknown' }],
    ['invalid date-time', { ...validInvoice, issuedAt: '30 July 2026' }],
    [
      'numeric pagination encoded as a string',
      { ...validInvoice, page: { ...validInvoice.page, page: '1' } },
    ],
  ])('rejects %s', (_description, malformed) => {
    expect(zGetInvoiceContractResponse.safeParse(malformed).success).toBe(false);
  });

  it('accepts a representative RFC Problem Details response', () => {
    expect(
      zProblemDetails.safeParse({
        type: 'https://example.invalid/problems/conflict',
        title: 'Conflict',
        status: 409,
        detail: 'The supplied version is stale.',
        instance: '/api/v1/invoices/42',
      }).success,
    ).toBe(true);
  });
});
