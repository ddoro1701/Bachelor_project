import React, { useMemo } from 'react';
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
    const columns = useMemo(() => [
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
                const dateValue = new Date(value);
                return isNaN(dateValue.getTime())
                    ? 'Invalid Date'
                    : dateValue.toLocaleDateString();
            },
        },
        {
            Header: 'Status',
            accessor: 'status',
            Cell: ({ value }) => (
                <span className={`badge ${value === 'Collected' ? 'badge-success' : 'badge-warn'}`}>
                    {value}
                </span>
            ),
        },
        {
            Header: 'Action',
            Cell: ({ row }) => (
                <button onClick={() => onToggleStatus(row.original)}>
                    {row.original.status === 'Received' ? 'Mark as Collected' : 'Mark as Received'}
                </button>
            ),
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
        const exportData = rows.map(row => ({
            "Lecturer Email": row.original.lecturerEmail,
            "Item Count": row.original.itemCount,
            "Shipping Provider": row.original.shippingProvider,
            "Additional Info": row.original.additionalInfo,
            "Collection Date": row.original.collectionDate ? new Date(row.original.collectionDate).toLocaleDateString() : '',
            "Status": row.original.status,
        }));
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
        if (!window.confirm(`Are you sure you want to delete ${collectedEntries.length} collected entries?`)) {
            return;
        }
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
                if (typeof onDeleteCollected === "function") {
                    onDeleteCollected(collectedEntries);
                }
                window.dispatchEvent(new CustomEvent('toast', { detail: { type: 'success', message: 'Collected entries deleted.' } }));
            })
            .catch(err => {
                console.error('Error deleting collected entries:', err);
                window.dispatchEvent(new CustomEvent('toast', { detail: { type: 'error', message: 'Error deleting collected entries.' } }));
            });
    };

    return (
        <div className="table-card card">
            <div className="table-wrapper">
                <table {...getTableProps()}>
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

            <div className="table-controls">
                <button onClick={() => gotoPage(0)} disabled={!canPreviousPage}>{'<<'}</button>
                <button onClick={() => previousPage()} disabled={!canPreviousPage}>Previous</button>

                {/* Page-Size Select zwischen Previous und Next, schmal */}
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