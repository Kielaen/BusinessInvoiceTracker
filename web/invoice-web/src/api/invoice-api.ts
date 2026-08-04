import type { z } from 'zod'
import { client } from '../generated/client.gen'
import {
  createWalkingSkeletonProbe,
  createCustomer as createCustomerRequest,
  createDraftInvoice as createDraftInvoiceRequest,
  getCustomer as getCustomerRequest,
  getDashboard as getDashboardRequest,
  getInvoice as getInvoiceRequest,
  getInvoiceAudit as getInvoiceAuditRequest,
  getAntiforgeryToken,
  getCurrentUser,
  getWalkingSkeletonProbe,
  listCustomers as listCustomersRequest,
  listInvoices as listInvoicesRequest,
  login,
  logout,
  issueInvoice as issueInvoiceRequest,
  recordInvoicePayment as recordInvoicePaymentRequest,
  updateCustomer as updateCustomerRequest,
  updateDraftInvoice as updateDraftInvoiceRequest,
  voidInvoice as voidInvoiceRequest,
} from '../generated/sdk.gen'
import type {
  CustomerCreateRequest,
  CustomerUpdateRequest,
  InvoiceCreateRequest,
  InvoiceCommandRequest,
  InvoicePaymentRequest,
  InvoiceUpdateRequest,
} from '../generated/types.gen'
import {
  zAntiforgeryResponse,
  zCurrentUserResponse,
  zCustomerPageResponse,
  zCustomerResponse,
  zDashboardResponse,
  zGetInvoiceAuditResponse,
  zInvoicePageResponse,
  zInvoiceResponse,
  zWalkingSkeletonAccepted,
  zWalkingSkeletonStatus,
} from '../generated/zod.gen'
import { ApiFailure, malformedPayload, responseFailure } from './failure'

client.setConfig({ credentials: 'same-origin' })

type CurrentUser = z.infer<typeof zCurrentUserResponse>
type LoginInput = { email: string; password: string }

function parse<T>(schema: z.ZodType<T>, payload: unknown): T {
  const result = schema.safeParse(payload)
  if (!result.success) throw malformedPayload(result.error)
  return result.data
}

async function antiforgeryHeaders(): Promise<Record<string, string>> {
  const result = await getAntiforgeryToken()
  if (result.error !== undefined) {
    throw responseFailure(result.response?.status ?? 500, result.error)
  }
  const token = parse(zAntiforgeryResponse, result.data)
  return { [token.headerName]: token.token }
}

export async function loadCurrentUser(): Promise<CurrentUser | null> {
  try {
    const result = await getCurrentUser()
    if (result.error !== undefined) {
      if (result.response?.status === 401) return null
      throw responseFailure(result.response?.status ?? 500, result.error)
    }
    return parse(zCurrentUserResponse, result.data)
  } catch (error) {
    if (error instanceof ApiFailure) throw error
    throw new ApiFailure('network', 'The service could not be reached.')
  }
}

export async function signIn(input: LoginInput): Promise<CurrentUser> {
  try {
    const result = await login({
      body: input,
      headers: await antiforgeryHeaders(),
    })
    if (result.error !== undefined) {
      throw responseFailure(result.response?.status ?? 500, result.error)
    }
    return parse(zCurrentUserResponse, result.data)
  } catch (error) {
    if (error instanceof ApiFailure) throw error
    throw new ApiFailure('network', 'The service could not be reached.')
  }
}

export async function signOut(): Promise<void> {
  try {
    const result = await logout({ headers: await antiforgeryHeaders() })
    if (result.error !== undefined) {
      throw responseFailure(result.response?.status ?? 500, result.error)
    }
  } catch (error) {
    if (error instanceof ApiFailure) throw error
    throw new ApiFailure('network', 'The service could not be reached.')
  }
}

export async function startDeliveryProbe() {
  const result = await createWalkingSkeletonProbe({
    headers: await antiforgeryHeaders(),
  })
  if (result.error !== undefined) {
    throw responseFailure(result.response?.status ?? 500, result.error)
  }
  return parse(zWalkingSkeletonAccepted, result.data)
}

export async function readDeliveryProbe(probeId: string) {
  const result = await getWalkingSkeletonProbe({ path: { probeId } })
  if (result.error !== undefined) {
    throw responseFailure(result.response?.status ?? 500, result.error)
  }
  return parse(zWalkingSkeletonStatus, result.data)
}

export async function loadCustomers(
  businessId: string,
  search: string,
  page: number,
  pageSize: number,
  signal?: AbortSignal,
) {
  const result = await listCustomersRequest({
    path: { businessId },
    query: { search: search || undefined, page, pageSize },
    signal,
  })
  if (signal?.aborted) throw new DOMException('The request was cancelled.', 'AbortError')
  if (result.error !== undefined) {
    throw responseFailure(result.response?.status ?? 500, result.error)
  }
  return parse(zCustomerPageResponse, result.data)
}

export async function loadCustomer(
  businessId: string,
  customerId: string,
  signal?: AbortSignal,
) {
  const result = await getCustomerRequest({
    path: { businessId, customerId },
    signal,
  })
  if (signal?.aborted) throw new DOMException('The request was cancelled.', 'AbortError')
  if (result.error !== undefined) {
    throw responseFailure(result.response?.status ?? 500, result.error)
  }
  return parse(zCustomerResponse, result.data)
}

export async function createCustomer(
  businessId: string,
  body: CustomerCreateRequest,
) {
  const result = await createCustomerRequest({
    path: { businessId },
    body,
    headers: await antiforgeryHeaders(),
  })
  if (result.error !== undefined) {
    throw responseFailure(result.response?.status ?? 500, result.error)
  }
  return parse(zCustomerResponse, result.data)
}

export async function updateCustomer(
  businessId: string,
  customerId: string,
  body: CustomerUpdateRequest,
) {
  const result = await updateCustomerRequest({
    path: { businessId, customerId },
    body,
    headers: await antiforgeryHeaders(),
  })
  if (result.error !== undefined) {
    throw responseFailure(result.response?.status ?? 500, result.error)
  }
  return parse(zCustomerResponse, result.data)
}

export async function loadInvoices(
  businessId: string,
  options: {
    search: string
    status: string
    sort: string
    direction: string
    page: number
    pageSize: number
  },
  signal?: AbortSignal,
) {
  const result = await listInvoicesRequest({
    path: { businessId },
    query: {
      search: options.search || undefined,
      status: options.status || undefined,
      sort: options.sort,
      direction: options.direction,
      page: options.page,
      pageSize: options.pageSize,
    },
    signal,
  })
  if (signal?.aborted) throw new DOMException('The request was cancelled.', 'AbortError')
  if (result.error !== undefined) throw responseFailure(result.response?.status ?? 500, result.error)
  return parse(zInvoicePageResponse, result.data)
}

export async function loadDashboard(businessId: string, signal?: AbortSignal) {
  const result = await getDashboardRequest({ path: { businessId }, signal })
  if (signal?.aborted) throw new DOMException('The request was cancelled.', 'AbortError')
  if (result.error !== undefined) throw responseFailure(result.response?.status ?? 500, result.error)
  return parse(zDashboardResponse, result.data)
}

export async function loadInvoice(
  businessId: string,
  invoiceId: string,
  signal?: AbortSignal,
) {
  const result = await getInvoiceRequest({ path: { businessId, invoiceId }, signal })
  if (signal?.aborted) throw new DOMException('The request was cancelled.', 'AbortError')
  if (result.error !== undefined) throw responseFailure(result.response?.status ?? 500, result.error)
  return parse(zInvoiceResponse, result.data)
}

export async function loadInvoiceAudit(
  businessId: string,
  invoiceId: string,
  signal?: AbortSignal,
) {
  const result = await getInvoiceAuditRequest({ path: { businessId, invoiceId }, signal })
  if (signal?.aborted) throw new DOMException('The request was cancelled.', 'AbortError')
  if (result.error !== undefined) throw responseFailure(result.response?.status ?? 500, result.error)
  return parse(zGetInvoiceAuditResponse, result.data)
}

export async function createDraftInvoice(businessId: string, body: InvoiceCreateRequest) {
  const result = await createDraftInvoiceRequest({
    path: { businessId }, body, headers: await antiforgeryHeaders(),
  })
  if (result.error !== undefined) throw responseFailure(result.response?.status ?? 500, result.error)
  return parse(zInvoiceResponse, result.data)
}

export async function updateDraftInvoice(
  businessId: string,
  invoiceId: string,
  body: InvoiceUpdateRequest,
) {
  const result = await updateDraftInvoiceRequest({
    path: { businessId, invoiceId }, body, headers: await antiforgeryHeaders(),
  })
  if (result.error !== undefined) throw responseFailure(result.response?.status ?? 500, result.error)
  return parse(zInvoiceResponse, result.data)
}

export async function issueInvoice(
  businessId: string,
  invoiceId: string,
  body: InvoiceCommandRequest,
) {
  const result = await issueInvoiceRequest({
    path: { businessId, invoiceId }, body, headers: await antiforgeryHeaders(),
  })
  if (result.error !== undefined) throw responseFailure(result.response?.status ?? 500, result.error)
  return parse(zInvoiceResponse, result.data)
}

export async function recordInvoicePayment(
  businessId: string,
  invoiceId: string,
  body: InvoicePaymentRequest,
) {
  const result = await recordInvoicePaymentRequest({
    path: { businessId, invoiceId }, body, headers: await antiforgeryHeaders(),
  })
  if (result.error !== undefined) throw responseFailure(result.response?.status ?? 500, result.error)
  return parse(zInvoiceResponse, result.data)
}

export async function voidInvoice(
  businessId: string,
  invoiceId: string,
  body: InvoiceCommandRequest,
) {
  const result = await voidInvoiceRequest({
    path: { businessId, invoiceId }, body, headers: await antiforgeryHeaders(),
  })
  if (result.error !== undefined) throw responseFailure(result.response?.status ?? 500, result.error)
  return parse(zInvoiceResponse, result.data)
}
