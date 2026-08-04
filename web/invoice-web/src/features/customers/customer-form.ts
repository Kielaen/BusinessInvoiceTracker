import type { CustomerResponse } from '../../generated/types.gen'

export type CustomerFormValue = {
  name: string
  email: string
  phone: string
  billingAddressLine1: string
  billingAddressLine2: string
  billingCity: string
  billingPostalCode: string
  billingCountryCode: string
}

export function emptyCustomerForm(): CustomerFormValue {
  return {
    name: '',
    email: '',
    phone: '',
    billingAddressLine1: '',
    billingAddressLine2: '',
    billingCity: '',
    billingPostalCode: '',
    billingCountryCode: 'ZA',
  }
}

export function customerToForm(customer: CustomerResponse): CustomerFormValue {
  return {
    name: customer.name,
    email: customer.email,
    phone: customer.phone ?? '',
    billingAddressLine1: customer.billingAddressLine1,
    billingAddressLine2: customer.billingAddressLine2 ?? '',
    billingCity: customer.billingCity,
    billingPostalCode: customer.billingPostalCode,
    billingCountryCode: customer.billingCountryCode,
  }
}

export function validateCustomerForm(value: CustomerFormValue) {
  const errors: Record<string, string> = {}
  if (!value.name.trim() || value.name.trim().length > 160)
    errors.name = 'Enter a name of 160 characters or fewer.'
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value.email) || value.email.length > 254)
    errors.email = 'Enter a valid email address.'
  if (value.phone.trim().length > 32) errors.phone = 'Use 32 characters or fewer.'
  if (!value.billingAddressLine1.trim() || value.billingAddressLine1.length > 200)
    errors.billingAddressLine1 = 'Enter address line 1.'
  if (value.billingAddressLine2.length > 200)
    errors.billingAddressLine2 = 'Use 200 characters or fewer.'
  if (!value.billingCity.trim() || value.billingCity.length > 120)
    errors.billingCity = 'Enter a city.'
  if (!value.billingPostalCode.trim() || value.billingPostalCode.length > 32)
    errors.billingPostalCode = 'Enter a postal code.'
  if (!/^[A-Za-z]{2}$/.test(value.billingCountryCode.trim()))
    errors.billingCountryCode = 'Enter a two-letter country code.'
  return errors
}
