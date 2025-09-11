import React, { useMemo, useState } from 'react';
import { useTable, useSortBy, usePagination, useFilters } from 'react-table';
import * as XLSX from 'xlsx';
import { saveAs } from 'file-saver';

function DefaultColumnFilter({ column: { filterValue, setFilter } }) {
    return (
        <input
            value={filterValue || ''}
            onChange={e => setFilter(e.target.value || undefined)}
            placeholder="Search..."
        />
    );
}

function LogTable({ data, onToggleStatus, onDeleteCollected }) {
    const [previewSrc, setPreviewSrc] = useState('');

    const columns = useMemo(() => [
        {
            Header: 'Image',
            accessor: 'imageUrl',
            disableFilters: true,
            Cell: ({ value }) => value ? (
                <img
                  src={value}
                  alt="label"
                  style={{ width: 56, height: 56, objectFit: 'cover', borderRadius: 6, cursor: 'zoom-in', border: '1px solid #ddd' }}
                  onClick={() => setPreviewSrc(value)}
                />
            ) : null
        },
        {
            Header: 'Lecturer Email',
            accessor: 'lecturerEmail',
        },
        {
            Header: 'Item Count',
            accessor: 'itemCount',
        },
        {
            Header: 'Shipping Provider',
            accessor: 'shippingProvider',
        },
        {
            Header: 'Additional Info',
            accessor: 'additionalInfo',
        },
        {
            Header: 'Collection Date',
            accessor: 'collectionDate',
            Cell: ({ value }) => {
                if (!value) return 'No Date';
                const dt = new Date(value);
                if (isNaN(dt.getTime())) return 'Invalid Date';
                return dt.toLocaleString(undefined, {
                  year: 'numeric', month: '2-digit', day: '2-digit',
                  hour: '2-digit', minute: '2-digit'
                });
            },
        },
        {
            Header: 'Status',
            accessor: 'status',
            Cell: ({ row, value }) => {
                const isCollected = value === 'Collected';
                const scannedAt = row.original.qrUsedAt || row.original.QrUsedAt; // Backend liefert camelCase
                const scannedDate = scannedAt ? new Date(scannedAt) : null;
                const title = scannedDate
                  ? `QR scanned: ${scannedDate.toLocaleString()}`
                  : (isCollected ? 'Collected' : 'Received');

                return (
                  <span
                    className={`status-pill ${isCollected ? 'collected' : 'received'}`}
                    title={title}
                  >
                    <span className="status-dot" aria-hidden="true"></span>
                    {value}
                    {scannedDate && (
                      <span className="qr-flag" title={title}>
                        <svg viewBox="0 0 24 24" className="qr-ico" aria-hidden="true">
                          <path fill="currentColor"
                            d="M3 3h8v8H3V3m2 2v4h4V5H5m6 6h2v2h-2v-2m4 0h6v6h-6v-6m2 2v2h2v-2h-2M3 13h8v8H3v-8m2 2v4h4v-4H5m10 4h2v2h-2v-2m4-4h2v6h-6v-2h4v-4Z"/>
                          </svg>
                        <span className="qr-ts">
                          {scannedDate.toLocaleString(undefined, { hour: '2-digit', minute: '2-digit', year: 'numeric', month: '2-digit', day: '2-digit' })}
                        </span>
                      </span>
                    )}
                  </span>
                );
            },
        },
        {
            Header: 'Action',
            Cell: ({ row }) => {
                const isCollected = row.original.status === 'Collected';
                return (
                  <button
                    className={`btn action ${isCollected ? 'make-received' : 'make-collected'}`}
                    onClick={() => onToggleStatus(row.original)}
                    title={isCollected ? 'Mark as Received' : 'Mark as Collected'}
                  >
                    {isCollected ? 'Set Received' : 'Mark Collected'}
                  </button>
                );
            },
        },
    ], [onToggleStatus]);

    const {
        getTableProps,
        getTableBodyProps,
        headerGroups,
        prepareRow,
        page,
        rows,
        canPreviousPage,
        canNextPage,
        pageOptions,
        pageCount,
        gotoPage,
        nextPage,
        previousPage,
        setPageSize,
        state: { pageIndex, pageSize },
    } = useTable(
        {
            columns,
            data,
            defaultColumn: { Filter: DefaultColumnFilter },
            initialState: { pageIndex: 0 },
        },
        useFilters,
        useSortBy,
        usePagination
    );

    const exportToExcel = () => {
        const exportData = rows.map(row => {
            const v = row.original.collectionDate ? new Date(row.original.collectionDate) : null;
            const when = v && !isNaN(v.getTime())
              ? v.toLocaleString(undefined, { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' })
              : '';
            return {
              "Lecturer Email": row.original.lecturerEmail,
              "Item Count": row.original.itemCount,
              "Shipping Provider": row.original.shippingProvider,
              "Additional Info": row.original.additionalInfo,
              "Collection Date": when,
              "Status": row.original.status,
            };
        });
        const worksheet = XLSX.utils.json_to_sheet(exportData, {
            header: ["Lecturer Email", "Item Count", "Shipping Provider", "Additional Info", "Collection Date", "Status"]
        });
        const workbook = XLSX.utils.book_new();
        XLSX.utils.book_append_sheet(workbook, worksheet, "Package Log");
        const today = new Date();
        const formattedDate = today.toISOString().split('T')[0];
        const fileName = `Package_Logs_${formattedDate}.xlsx`;
        const excelBuffer = XLSX.write(workbook, { bookType: "xlsx", type: "array" });
        const dataBlob = new Blob([excelBuffer], { type: "application/octet-stream" });
        saveAs(dataBlob, fileName);
    };

    const handleDeleteCollected = () => {
        const collectedEntries = page
          .filter(row => row.original.status === "Collected")
          .map(row => row.original.id);

        if (collectedEntries.length === 0) {
          window.dispatchEvent(new CustomEvent('toast', { detail: { type: 'info', message: 'No collected entries in current view.' } }));
          return;
        }

        const n = collectedEntries.length;
        window.dispatchEvent(new CustomEvent('confirm', {
          detail: {
            message: `Delete ${n} collected ${n === 1 ? 'entry' : 'entries'}?`,
            confirmText: 'Delete',
            cancelText: 'Cancel',
            onConfirm: () => {
              window.dispatchEvent(new CustomEvent('notice', {
                detail: { type: 'warning', message: `Deleting ${n} collected ${n === 1 ? 'entry' : 'entries'}...` }
              }));

              fetch('https://wrexhamuni-ocr-webapp-deeaeydrf2fdcfdy.uksouth-01.azurewebsites.net/api/package/delete-collected', {
                  method: 'DELETE',
                  headers: { 'Content-Type': 'application/json' },
                  body: JSON.stringify({ ids: collectedEntries })
              })
                  .then(res => {
                      if (!res.ok) throw new Error('Failed to delete collected entries');
                      return res.json();
                  })
                  .then(() => {
                      if (typeof onDeleteCollected === "function") onDeleteCollected(collectedEntries);
                      window.dispatchEvent(new CustomEvent('toast', { detail: { type: 'success', message: 'Collected entries deleted.' } }));
                  })
                  .catch(err => {
                      console.error('Error deleting collected entries:', err);
                      window.dispatchEvent(new CustomEvent('toast', { detail: { type: 'error', message: 'Error deleting collected entries.' } }));
                  });
            }
          }
        }));
    };

    return (
      <div>
        <div className="table-wrapper">
          <table {...getTableProps()} style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
                {headerGroups.map(headerGroup => (
                    <tr {...headerGroup.getHeaderGroupProps()}>
                        {headerGroup.headers.map(column => (
                            <th {...column.getHeaderProps()}>
                                <div className="th-head">
                                    <span className="th-title">
                                        {column.render('Header') === 'Action' ? '' : column.render('Header')}
                                    </span>
                                    {/* Sort nur über Icon-Button */}
                                    {column.render('Header') !== 'Action' && (
                                        <button
                                            className="sort-btn"
                                            {...column.getSortByToggleProps()}
                                            title={column.isSorted ? (column.isSortedDesc ? 'Sorted desc' : 'Sorted asc') : 'Sort'}
                                        >
                                            {column.isSorted ? (column.isSortedDesc ? '▼' : '▲') : '↕'}
                                        </button>
                                    )}
                                </div>
                                {column.canFilter ? (
                                    <div className="th-filter">{column.render('Filter')}</div>
                                ) : null}
                            </th>
                        ))}
                    </tr>
                ))}
            </thead>
            <tbody {...getTableBodyProps()}>
                {page.map(row => {
                    prepareRow(row);
                    const status = row.original.status;
                    const rowClass = status === 'Collected'
                        ? 'row-collected'
                        : status === 'Received'
                            ? 'row-received'
                            : '';
                    return (
                        <tr {...row.getRowProps()} className={rowClass}>
                            {row.cells.map(cell => (
                                <td
                                    {...cell.getCellProps()}
                                    data-label={cell.column.Header}   // für Mobile Cards
                                >
                                    {cell.render('Cell')}
                                </td>
                            ))}
                        </tr>
                    );
                })}
            </tbody>
          </table>
        </div>

        {/* Lightbox Preview */}
        {previewSrc && (
          <div
            onClick={() => setPreviewSrc('')}
            style={{
              position: 'fixed', inset: 0, background: 'rgba(0,0,0,.75)',
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              zIndex: 9999
            }}
          >
            <img
              src={previewSrc}
              alt="full"
              onClick={e => e.stopPropagation()}
              style={{ maxWidth: '90vw', maxHeight: '90vh', boxShadow: '0 10px 40px rgba(0,0,0,.6)', borderRadius: 8 }}
            />
          </div>
        )}

        <div style={{ marginTop: '10px' }}>
            <button onClick={() => gotoPage(0)} disabled={!canPreviousPage}>{'<<'}</button>
            <button onClick={() => previousPage()} disabled={!canPreviousPage}>Previous</button>

            {/* Page-Size Select zwischen Previous and Next, schmal */}
            <select
                className="page-size"
                value={pageSize}
                onChange={e => {
                    setPageSize(Number(e.target.value));
                    gotoPage(0);
                }}
            >
                {[5, 10, 20, 50, 100, 200].map(size => (
                    <option key={size} value={size}>
                        Show {size}
                    </option>
                ))}
            </select>

            <button onClick={() => nextPage()} disabled={!canNextPage}>Next</button>
            <button onClick={() => gotoPage(pageCount - 1)} disabled={!canNextPage}>{'>>'}</button>

            <span>Page <strong>{pageIndex + 1} of {pageOptions.length}</strong></span>

            <button onClick={exportToExcel}>Export to Excel</button>
            <button onClick={handleDeleteCollected}>Delete Collected</button>
        </div>
      </div>
    );
}

export default LogTable;