#include "main.h"

unordered_map<size_t, vector<void*>*> pools;
StringMemory *strmem;

void init_pools( void )
{
	pools.reserve( 1000 );
	
	strmem = (StringMemory*)halloc( sizeof(StringMemory) );
	new(strmem) StringMemory();
}

void *halloc( size_t sz )
{
	unordered_map<size_t, vector<void*>*>::iterator it;
	vector <void*> *pool;
	if( (it=pools.find(sz)) != pools.end() ) {
		pool = it->second;
		if( pool->size() > 0 ) {
			void *ptr = pool->back();
			pool->pop_back();
			return ptr;
		}
	}
	return malloc(sz);
}
void hfree( void *ptr, size_t sz )
{
	unordered_map<size_t, vector<void*>*>::iterator it;
	vector <void*> *pool;
	if( (it=pools.find(sz)) != pools.end() ) {
		pool = it->second;
		pool->push_back(ptr);
	} else {
		pool = new vector<void*>();
		pool->push_back(ptr);
		pools[sz] = pool;
	}
}

StringMemory::StringMemory()
{
	size_t size = 1024*1024;
	char *zero = (char*)malloc( size );

	StringMemoryItem sptr(zero, size);
	StringMemoryItem2 ssz(zero, size);

	items_ptr.insert( sptr );
	items_sz.insert( ssz );
}

StringMemory::~StringMemory()
{
}


StringMemoryItem::StringMemoryItem( const StringMemoryItem2 &a )
: ptr(a.ptr), size(a.size)
{
//	lprintf("Built %p from %p", ptr, a.ptr);
}

char *StringMemory::Realloc( char *ptr, size_t orig_sz, size_t new_sz )
{
	char *np = Alloc( new_sz );

	if( orig_sz != 0 ) {
		memcpy( np, ptr, orig_sz );
		Free( ptr, orig_sz );
	}

	return np;
}

unsigned char *StringMemory::Realloc( unsigned char *ptr, size_t orig_sz, size_t new_sz )
{
	unsigned char *np = (unsigned char*)Alloc( new_sz );

	if( orig_sz != 0 ) {
		memcpy( np, ptr, orig_sz );
		Free( (char*)ptr, orig_sz );
	}

	return np;
}
char *StringMemory::ReallocStr( char *ptr, size_t orig_sz, size_t new_sz )
{
	char *np = Alloc( new_sz );

	if( orig_sz != 0 ) {
		strcpy( np, ptr );
		Free( ptr, orig_sz );
	}

	return np;
}

// TODO: Support freeing memory by tracking which blocks are allocated and which are from the middle of another pool.
// Then we can deallocate things that are allocated to restore memory.
char *StringMemory::Alloc( size_t sz )
{
	char *ptr;
	/*
	set<StringMemoryItem2>::iterator it;
	unsigned int sz1, sz2;

	sz1 = (unsigned int) items_sz.size();
	//lprintf("Alloc: Searching in %u blocks", sz1);

	it = items_sz.lower_bound( StringMemoryItem2(NULL, sz) );
	if( it != items_sz.end() ) {
		const StringMemoryItem2 &item = *it;
		sz1 = (unsigned int)item.size;
		sz2 = (unsigned int)sz;
		//lprintf("Found matching memory block %p of size %u looking for %u", item.ptr, sz1, sz2);
		StringMemoryItem2 newitem;
		StringMemoryItem item1;
		set<StringMemoryItem>::iterator it1;

		ptr = item.ptr;
		item1 = StringMemoryItem(item);
		items_sz.erase( it );
		sz1 = (unsigned int)item1.size;
		it1 = items_ptr.find(item1);
		if( it1 == items_ptr.end() ) {
			lprintf("Error: unmatched memory: cannot find size %u at %p", sz1, item1.ptr);
			abort();
		}
		if( item1.size > sz ) {
			newitem.ptr = item1.ptr + sz;
			newitem.size = item1.size - sz;
			if( newitem.size < 24 ) {
				lprintf("drop small block");
				// free( newitem.ptr ); no. we can't free it, it's in the middle of a block. we'll just let it go.
			} else {
				items_sz.insert( newitem );
				items_ptr.insert( StringMemoryItem(newitem) );
			}
		}
		items_ptr.erase( it1 );
		return ptr;
	}
	*/
	
	// no free block found, allocate new
	ptr = (char*)malloc( sz );
	/*
	StringMemoryItem sptr(ptr, sz);
	StringMemoryItem2 ssz(ptr, sz);
	items_ptr.insert( sptr );
	items_sz.insert( ssz );
	*/
	return ptr;
}

void StringMemory::Free( char *ptr, size_t sz )
{
	/*
	set<StringMemoryItem>::iterator it;
	set<StringMemoryItem2>::iterator it2;
	StringMemoryItem srch(ptr, sz);

	unsigned int sz1 = (unsigned int)sz;
	lprintf("Release %u bytes from %p", sz1, ptr);
	
	it = items_ptr.lower_bound( srch );
	if( it != items_ptr.end() ) {
		const StringMemoryItem &item = *it;
		if( ptr + sz == item.ptr ) {
			StringMemoryItem newitem(item);
			it2 = items_sz.find( item );
			if( it2 == items_sz.end() ) {
				lprintf("Error: unmatched memory: cannot find size %lu at %p", item.size, item.ptr);
				abort();
			}
			items_sz.erase( it2 );
			items_ptr.erase( it );

			newitem.ptr = ptr;
			newitem.size += sz;
			items_sz.insert( StringMemoryItem2(newitem) );
			items_ptr.insert( newitem );
			return;
		}
		if( it != items_ptr.begin() ) {
			it--;
			if( it != items_ptr.end() ) {
				const StringMemoryItem &itemb = *it;
				if( itemb.ptr + itemb.size == ptr ) {
					StringMemoryItem newitem(itemb);

					it2 = items_sz.find(itemb);
					if( it2 == items_sz.end() ) {
						lprintf("Error: unmatched memory: cannot find size %lu at %p", itemb.size, itemb.ptr);
						abort();
					}
					items_sz.erase( it2 );
					items_ptr.erase( it );

					newitem.size += sz;
					items_sz.insert( StringMemoryItem2(newitem) );
					items_ptr.insert( newitem );
					return;
				}
			}
		}
	}
	items_ptr.insert( srch );
	items_sz.insert( StringMemoryItem2(srch) );
	*/
	free(ptr);
}

/*
StringTrie::StringTrie()
{
	str = NULL;
	for( int i=0; i<27; i++ ) {
		t[i] = NULL;
	}
}

StringTrie::~StringTrie()
{
	for( int i=0; i<27; i++ ) {
		if( t[i] ) {
			delete t[i];
		}
	}
}

StringTrie::Add(const char *str, int offset)
{
	int i, c;
	const char *ptr;
	StringTrie *st = this;

	for( ptr = str+offset; ptr; ptr++ ) {
		c = *ptr;
		if( c >= 'a' && c <= 'z' ) {
			c -= 'a';
		} else if( c >= 'A' && c <= 'Z' ) {
			c -= 'A';
		} else {
			c = 26;
		}
		if( !st->t[c] ) {
			st->t[c] = new StringTrie();
			st->t[c]->str = str;
			st = t[c];
		}
	}
}
*/
