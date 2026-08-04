import type { InvoiceResponse } from '../../generated/types.gen'

export type InvoiceLineForm = {
  description: string
  quantity: string
  unitPrice: string
  taxRate: string
}

export type InvoiceFormValue = {
  customerId: string
  currency: string
  issueDate: string
  dueDate: string
  lines: InvoiceLineForm[]
}

const today = () => new Date().toISOString().slice(0, 10)

export const emptyInvoiceForm = (): InvoiceFormValue => ({
  customerId: '', currency: 'ZAR', issueDate: today(), dueDate: today(),
  lines: [{ description: '', quantity: '1', unitPrice: '0.00', taxRate: '15' }],
})

export const invoiceToForm = (invoice: InvoiceResponse): InvoiceFormValue => ({
  customerId: invoice.customerId,
  currency: invoice.currency,
  issueDate: invoice.issueDate,
  dueDate: invoice.dueDate,
  lines: invoice.lines.map((line) => ({
    description: line.description,
    quantity: String(line.quantity),
    unitPrice: String(line.unitPrice),
    taxRate: String(line.taxRate),
  })),
})

export const validateInvoice = (value: InvoiceFormValue) => {
  const errors: string[] = []
  if (!value.customerId) errors.push('Select a customer.')
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value.issueDate)) errors.push('Enter an issue date.')
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value.dueDate)) errors.push('Enter a due date.')
  if (value.dueDate < value.issueDate) errors.push('Due date cannot be before issue date.')
  if (value.lines.length < 1 || value.lines.length > 200) errors.push('Add between 1 and 200 lines.')
  value.lines.forEach((line, index) => {
    if (!line.description.trim()) errors.push(`Line ${index + 1} needs a description.`)
    const quantity = Number(line.quantity)
    const unitPrice = Number(line.unitPrice)
    const taxRate = Number(line.taxRate)
    if (!Number.isFinite(quantity) || quantity <= 0) errors.push(`Line ${index + 1} quantity must be positive.`)
    if (!Number.isFinite(unitPrice) || unitPrice < 0) errors.push(`Line ${index + 1} unit price cannot be negative.`)
    if (!Number.isFinite(taxRate) || taxRate < 0 || taxRate > 100) errors.push(`Line ${index + 1} tax must be 0 to 100.`)
  })
  return errors
}

export const invoiceWriteBody = (value: InvoiceFormValue) => ({
  customerId: value.customerId,
  currency: value.currency,
  issueDate: value.issueDate,
  dueDate: value.dueDate,
  lines: value.lines.map((line) => ({
    description: line.description.trim(),
    quantity: Number(line.quantity),
    unitPrice: Number(line.unitPrice),
    taxRate: Number(line.taxRate),
  })),
})
